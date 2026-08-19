// «Жидкое» искажение фото при наведении: WebGL-шейдер (по образцу water.js,
// без библиотек). Один общий канвас накладывается поверх фото под курсором:
// от точки курсора расходится волна-рябь с лёгким зумом и хроматической
// аберрацией, сила эффекта плавно нарастает и спадает (lerp). При нулевой
// силе шейдер отдаёт исходную картинку в режиме cover — подмена незаметна.
// Канвас с pointer-events: none — клики (лайтбокс, переход на место) проходят.
// Только pointer: fine, без prefers-reduced-motion; без WebGL просто ничего
// не происходит — остаётся обычный CSS-hover.
(function () {
    "use strict";

    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
    if (!window.matchMedia("(pointer: fine)").matches) return;

    var imgs = Array.prototype.slice.call(
        document.querySelectorAll(".place-photo img, .detail-photo img"));
    if (!imgs.length) return;

    var canvas = document.createElement("canvas");
    canvas.className = "photo-fx";
    document.body.appendChild(canvas);

    var gl = canvas.getContext("webgl", { alpha: false, antialias: false, depth: false });
    if (!gl) return;

    var VS = "attribute vec2 p;void main(){gl_Position=vec4(p,0.,1.);}";
    var FS = [
        "precision mediump float;",
        "uniform sampler2D tex;",
        "uniform vec2 res;",      // размер канваса, px
        "uniform vec2 mouse;",    // курсор в uv (0..1, y вниз)
        "uniform float t;",       // время, c
        "uniform float s;",       // сила эффекта 0..1
        "uniform vec2 uvScale;",  // cover-маппинг текстуры
        "uniform vec2 uvOff;",
        "void main(){",
        "  vec2 uv = gl_FragCoord.xy / res;",
        "  uv.y = 1.0 - uv.y;",
        "  vec2 d = uv - mouse;",
        "  float dist = length(d * vec2(res.x / res.y, 1.0));",
        "  float fall = exp(-dist * 4.5);",                      // затухание от курсора
        "  vec2 dir = normalize(d + 1e-4);",
        "  vec2 disp = dir * sin(dist * 22.0 - t * 5.0) * 0.012 * fall * s;",
        "  vec2 zoomUv = mouse + (uv - mouse) * (1.0 - 0.05 * s * fall);", // лёгкий зум к курсору
        "  vec2 fuv = (zoomUv + disp) * uvScale + uvOff;",
        "  float ca = 0.0035 * s * fall;",                       // хроматическая аберрация
        "  float r = texture2D(tex, fuv + dir * ca).r;",
        "  float g = texture2D(tex, fuv).g;",
        "  float b = texture2D(tex, fuv - dir * ca).b;",
        "  gl_FragColor = vec4(r, g, b, 1.0);",
        "}"
    ].join("\n");

    function shader(type, src) {
        var sh = gl.createShader(type);
        gl.shaderSource(sh, src);
        gl.compileShader(sh);
        return gl.getShaderParameter(sh, gl.COMPILE_STATUS) ? sh : null;
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

    var U = {};
    ["res", "mouse", "t", "s", "uvScale", "uvOff"].forEach(function (n) {
        U[n] = gl.getUniformLocation(prog, n);
    });

    // Текстуры кэшируются по элементу <img>. Фото пережимается в POT-квадрат
    // 1024×1024 (аспект не важен — uv нормализованы), чтобы включить мипмапы:
    // без них LINEAR-минификация 1280px → ~800px даёт зернистый алиасинг.
    var texCache = new WeakMap();
    var potCanvas = document.createElement("canvas");
    potCanvas.width = potCanvas.height = 1024;
    var potCtx = potCanvas.getContext("2d");
    function texture(img) {
        var tx = texCache.get(img);
        if (tx) return tx;
        potCtx.drawImage(img, 0, 0, 1024, 1024);
        tx = gl.createTexture();
        gl.bindTexture(gl.TEXTURE_2D, tx);
        gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGB, gl.RGB, gl.UNSIGNED_BYTE, potCanvas);
        gl.generateMipmap(gl.TEXTURE_2D);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR_MIPMAP_LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        texCache.set(img, tx);
        return tx;
    }

    var dpr = Math.min(window.devicePixelRatio || 1, 2);
    var active = null;   // { img, mx, my }
    var strength = 0, strengthT = 0, running = false, t0 = performance.now();

    function place(img) {
        var r = img.getBoundingClientRect();
        canvas.style.left = r.left + "px";
        canvas.style.top = r.top + "px";
        canvas.style.width = r.width + "px";
        canvas.style.height = r.height + "px";
        var w = Math.round(r.width * dpr), h = Math.round(r.height * dpr);
        if (canvas.width !== w || canvas.height !== h) {
            canvas.width = w;
            canvas.height = h;
            gl.viewport(0, 0, w, h);
        }
        return r;
    }

    function frame() {
        if (!active && strength < 0.01) {
            running = false;
            canvas.classList.remove("active");
            return;
        }
        strength += (strengthT - strength) * 0.09;
        var img = (active || frame.last).img;
        frame.last = active || frame.last;
        var r = place(img);
        // cover-маппинг: та же обрезка, что у object-fit: cover
        var ia = img.naturalWidth / img.naturalHeight, ra = r.width / r.height;
        var sx = 1, sy = 1;
        if (ia > ra) sx = ra / ia; else sy = ia / ra;
        gl.bindTexture(gl.TEXTURE_2D, texture(img));
        gl.uniform2f(U.res, canvas.width, canvas.height);
        gl.uniform2f(U.mouse, frame.last.mx, frame.last.my);
        gl.uniform1f(U.t, (performance.now() - t0) / 1000);
        gl.uniform1f(U.s, strength);
        gl.uniform2f(U.uvScale, sx, sy);
        gl.uniform2f(U.uvOff, (1 - sx) / 2, (1 - sy) / 2);
        gl.drawArrays(gl.TRIANGLES, 0, 3);
        requestAnimationFrame(frame);
    }

    imgs.forEach(function (img) {
        var host = img.closest("a") || img;
        host.addEventListener("pointerenter", function (e) {
            if (!img.complete || !img.naturalWidth) return;
            var r = img.getBoundingClientRect();
            active = { img: img, mx: (e.clientX - r.left) / r.width, my: (e.clientY - r.top) / r.height };
            strengthT = 1;
            // Скругления: радиус лежит на обрезающем предке (.place-card через
            // overflow hidden либо .detail-photo). Применяем его к канвасу
            // по-угловно — только там, где угол фото совпадает с углом предка:
            // нижние углы фото внутри карточки остаются прямыми.
            var clip = img.closest(".detail-photo, .place-card") || img;
            var cs = getComputedStyle(clip);
            var cr = clip.getBoundingClientRect();
            var near = function (a, b) { return Math.abs(a - b) < 4; };
            canvas.style.borderRadius =
                (near(r.left, cr.left) && near(r.top, cr.top) ? cs.borderTopLeftRadius : "0px") + " " +
                (near(r.right, cr.right) && near(r.top, cr.top) ? cs.borderTopRightRadius : "0px") + " " +
                (near(r.right, cr.right) && near(r.bottom, cr.bottom) ? cs.borderBottomRightRadius : "0px") + " " +
                (near(r.left, cr.left) && near(r.bottom, cr.bottom) ? cs.borderBottomLeftRadius : "0px");
            canvas.classList.add("active");
            if (!running) { running = true; requestAnimationFrame(frame); }
        });
        host.addEventListener("pointermove", function (e) {
            if (!active || active.img !== img) return;
            var r = img.getBoundingClientRect();
            active.mx = (e.clientX - r.left) / r.width;
            active.my = (e.clientY - r.top) / r.height;
        });
        host.addEventListener("pointerleave", function () {
            if (active && active.img === img) { active = null; strengthT = 0; }
        });
    });
}());
