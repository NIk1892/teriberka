// Вода у «берега» перед футером: маленький WebGL-шейдер без зависимостей.
// Волнующаяся поверхность с бликами; цвета берутся из CSS-переменных темы.
// Если WebGL недоступен или включён prefers-reduced-motion — канвас остаётся
// пустым и снизу видны CSS-волны (фоллбек).
(function () {
    "use strict";

    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;

    var canvas = document.querySelector(".water-canvas");
    if (!canvas) return;

    var gl = canvas.getContext("webgl", { alpha: true, antialias: false, depth: false });
    if (!gl) return;

    // ---- цвета из темы (--accent-ice и фон) -> RGB 0..1
    function cssColor(name, fallback) {
        var v = getComputedStyle(document.documentElement).getPropertyValue(name).trim() || fallback;
        var m = v.match(/^#([0-9a-f]{6})$/i);
        if (!m) return fallback;
        var n = parseInt(m[1], 16);
        return [((n >> 16) & 255) / 255, ((n >> 8) & 255) / 255, (n & 255) / 255];
    }
    var glint = cssColor("--accent-ice", [0.30, 0.79, 0.94]);
    var deepC = document.documentElement.dataset.theme === "light"
        ? [0.55, 0.71, 0.85]
        : [0.03, 0.07, 0.16];

    var VS = "attribute vec2 p;void main(){gl_Position=vec4(p,0.,1.);}";
    var FS = [
        "precision mediump float;",
        "uniform vec2 r;uniform float t;uniform float px;uniform vec3 deep;uniform vec3 glint;",
        "float wave(float x){",
        "  return sin(x*6.3+t*.9)*.32+sin(x*11.7-t*1.25+1.7)*.2",
        "       +sin(x*21.0+t*1.8+4.1)*.11+sin(x*34.0-t*2.6+2.3)*.06;",
        "}",
        "void main(){",
        "  vec2 uv=gl_FragCoord.xy/r;",
        "  float yTop=r.y-gl_FragCoord.y;",                 // px от верха зоны
        "  float surfY=(60.0+wave(uv.x)*12.0)*px;",         // поверхность воды, px
        "  float dpx=yTop-surfY;",
        "  if(dpx<0.){gl_FragColor=vec4(0.);return;}",
        "  float depth=clamp(dpx/(420.0*px),0.,1.);",
        "  vec3 col=mix(mix(glint,deep,.6),deep,sqrt(depth));",
        "  col*=1.0+.06*sin(dpx/(14.0*px)-t*.7+wave(uv.x)*2.5);",
        "  float surfZone=exp(-dpx/(46.0*px));",
        "  float slope=wave(uv.x+.008)-wave(uv.x-.008);",
        "  float glare=smoothstep(.015,.05,slope)*surfZone*.22;",
        "  float foam=smoothstep(6.0*px,0.,dpx)*.45;",
        "  col+=glint*(glare+foam);",
        "  float a=mix(.92,.8,depth)*smoothstep(0.,6.0*px,dpx);",
        "  gl_FragColor=vec4(col*a,a);",
        "}"
    ].join("\n");

    function shader(type, src) {
        var s = gl.createShader(type);
        gl.shaderSource(s, src);
        gl.compileShader(s);
        return gl.getShaderParameter(s, gl.COMPILE_STATUS) ? s : null;
    }
    var vs = shader(gl.VERTEX_SHADER, VS);
    var fs = shader(gl.FRAGMENT_SHADER, FS);
    if (!vs || !fs) return;
    var prog = gl.createProgram();
    gl.attachShader(prog, vs);
    gl.attachShader(prog, fs);
    gl.linkProgram(prog);
    if (!gl.getProgramParameter(prog, gl.LINK_STATUS)) return;
    gl.useProgram(prog);

    gl.bindBuffer(gl.ARRAY_BUFFER, gl.createBuffer());
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 3, -1, -1, 3]), gl.STATIC_DRAW);
    var loc = gl.getAttribLocation(prog, "p");
    gl.enableVertexAttribArray(loc);
    gl.vertexAttribPointer(loc, 2, gl.FLOAT, false, 0, 0);

    var uR = gl.getUniformLocation(prog, "r");
    var uT = gl.getUniformLocation(prog, "t");
    var uPx = gl.getUniformLocation(prog, "px");
    gl.uniform3fv(gl.getUniformLocation(prog, "deep"), deepC);
    gl.uniform3fv(gl.getUniformLocation(prog, "glint"), glint);

    gl.enable(gl.BLEND);
    gl.blendFunc(gl.ONE, gl.ONE_MINUS_SRC_ALPHA);

    function resize() {
        var dpr = Math.min(window.devicePixelRatio || 1, 1.5);
        var w = Math.round(canvas.clientWidth * dpr);
        var h = Math.round(canvas.clientHeight * dpr);
        if (canvas.width !== w || canvas.height !== h) {
            canvas.width = w;
            canvas.height = h;
            gl.viewport(0, 0, w, h);
        }
        gl.uniform2f(uR, canvas.width, canvas.height);
        gl.uniform1f(uPx, dpr);
    }
    window.addEventListener("resize", resize);
    resize();

    // WebGL запустился — CSS-волны-фоллбек больше не нужны
    var zone = canvas.closest(".sea-zone");
    if (zone) zone.classList.add("water-live");

    // рисуем только когда полоса видна на экране
    var visible = false, raf = 0, start = performance.now();
    function frame(now) {
        raf = 0;
        if (!visible) return;
        gl.uniform1f(uT, (now - start) / 1000);
        gl.drawArrays(gl.TRIANGLES, 0, 3);
        raf = requestAnimationFrame(frame);
    }
    new IntersectionObserver(function (entries) {
        visible = entries[0].isIntersecting;
        if (visible && !raf) raf = requestAnimationFrame(frame);
    }).observe(canvas);
})();
