// Инерционный параллакс для полос .parallax-band: слои «догоняют» прокрутку
// с запаздыванием (lerp), плюс лёгкий отклик на движение мыши на десктопе.
// Скрипт пишет только CSS-переменные --p-* на полосе; сами трансформы
// применяет app.css по классу html.js-parallax. Без JS остаётся CSS-фоллбек
// (scroll-driven animations или fixed-фон), при prefers-reduced-motion
// скрипт не запускается вовсе. Расчёт идёт только пока полоса видна
// (IntersectionObserver) — по тому же образцу, что water.js.
(function () {
    "use strict";

    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;

    var bands = Array.prototype.slice.call(document.querySelectorAll(".parallax-band"));
    if (!bands.length) return;

    document.documentElement.classList.add("js-parallax");

    // Амплитуды вертикального хода, px: плюс — слой отстаёт от прокрутки
    // (читается дальше), минус — обгоняет (ближе). Согласованы с
    // --parallax-shift CSS-фоллбека, но крупнее — инерция сглаживает ход.
    var AMP = { far: 110, near: 40, mist: 68, text: -30 };
    // Амплитуды отклика на мышь, px (слои уходят против курсора, ближе = сильнее)
    var AMX = { far: 10, near: 22, mist: 30 };

    var state = bands.map(function (el) {
        return { el: el, cur: 0, visible: false };
    });

    var hasFine = window.matchMedia("(pointer: fine)").matches;
    var mouseT = 0, mouseC = 0; // цель и текущее значение, −1..1
    if (hasFine) {
        window.addEventListener("pointermove", function (e) {
            mouseT = (e.clientX / window.innerWidth) * 2 - 1;
        }, { passive: true });
    }

    var running = false;

    function frame() {
        var vh = window.innerHeight;
        var active = false;
        mouseC += (mouseT - mouseC) * 0.05;
        for (var i = 0; i < state.length; i++) {
            var s = state[i];
            if (!s.visible) continue;
            active = true;
            var r = s.el.getBoundingClientRect();
            var t = (vh - r.top) / (vh + r.height); // 0 — вошла снизу, 1 — ушла вверх
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            s.cur += (t - s.cur) * 0.12;            // инерция (Lenis уже сглаживает
                                                    // прокрутку — лаг не задваиваем)
            var y = (s.cur - 0.5) * 2;              // −1..1
            var st = s.el.style;
            st.setProperty("--p-far", (y * AMP.far).toFixed(1) + "px");
            st.setProperty("--p-near", (y * AMP.near).toFixed(1) + "px");
            st.setProperty("--p-mist", (y * AMP.mist).toFixed(1) + "px");
            st.setProperty("--p-text", (y * AMP.text).toFixed(1) + "px");
            if (hasFine) {
                st.setProperty("--p-farx", (-mouseC * AMX.far).toFixed(1) + "px");
                st.setProperty("--p-nearx", (-mouseC * AMX.near).toFixed(1) + "px");
                st.setProperty("--p-mistx", (-mouseC * AMX.mist).toFixed(1) + "px");
            }
        }
        if (active) requestAnimationFrame(frame);
        else running = false;
    }

    function wake() {
        if (!running) {
            running = true;
            requestAnimationFrame(frame);
        }
    }

    var io = new IntersectionObserver(function (entries) {
        for (var i = 0; i < entries.length; i++) {
            for (var j = 0; j < state.length; j++) {
                if (state[j].el === entries[i].target) {
                    state[j].visible = entries[i].isIntersecting;
                }
            }
        }
        wake();
    }, { rootMargin: "160px 0px" });

    state.forEach(function (s) { io.observe(s.el); });
}());
