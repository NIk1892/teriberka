// Карусели (галереи страниц мест, фото в hero). Прогрессивное улучшение:
// без скрипта кнопки скрыты, карусель полностью работает свайпом/колесом
// (scroll-snap). Обслуживает каждый .carousel-wrap с треком .carousel-track;
// стрелки необязательны — в hero только автоматическая смена кадров и свайп.
// data-autoplay="<мс>" на обёртке добавляет автопрокрут по правилам проекта:
// при prefers-reduced-motion не стартует, пауза вне вьюпорта / в фоновой вкладке /
// под курсором, любое ручное листание выключает его насовсем.
(function () {
    "use strict";

    document.querySelectorAll(".carousel-wrap").forEach(function (wrap) {
        var track = wrap.querySelector(".carousel-track");
        var prev = wrap.querySelector(".car-prev");
        var next = wrap.querySelector(".car-next");
        if (!track) return;

        var smooth = window.matchMedia("(prefers-reduced-motion: reduce)").matches ? "auto" : "smooth";

        function step() {
            var item = track.firstElementChild;
            if (!item) return track.clientWidth;
            var gap = parseFloat(getComputedStyle(track).columnGap) || 0;
            return item.getBoundingClientRect().width + gap;
        }

        function update() {
            var max = track.scrollWidth - track.clientWidth - 1;
            if (prev) prev.disabled = track.scrollLeft <= 1;
            if (next) next.disabled = track.scrollLeft >= max;
        }

        if (prev) prev.addEventListener("click", function () {
            track.scrollBy({ left: -step(), behavior: smooth });
        });
        if (next) next.addEventListener("click", function () {
            track.scrollBy({ left: step(), behavior: smooth });
        });
        track.addEventListener("scroll", update, { passive: true });
        // Клавиши листают по кадру только у треков с явным tabindex.
        // События ссылок и полей внутри других каруселей не перехватываем.
        track.addEventListener("keydown", function (event) {
            if (event.target !== track) return;
            if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") return;
            event.preventDefault();
            track.scrollBy({ left: event.key === "ArrowRight" ? step() : -step(), behavior: smooth });
        });
        window.addEventListener("resize", update);

        wrap.classList.add("carousel-js");
        update();

        var delay = parseInt(wrap.dataset.autoplay || "", 10);
        if (!delay || window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;

        var timer = null;
        var visible = false;
        var hovered = false;
        var stopped = false;

        function tick() {
            var max = track.scrollWidth - track.clientWidth - 1;
            if (track.scrollLeft >= max) track.scrollTo({ left: 0, behavior: smooth });
            else track.scrollBy({ left: step(), behavior: smooth });
        }

        function sync() {
            var run = visible && !hovered && !stopped && !document.hidden;
            if (run && timer === null) timer = window.setInterval(tick, delay);
            else if (!run && timer !== null) { window.clearInterval(timer); timer = null; }
        }

        // Человек взялся за карусель (свайп, колесо, стрелки, фокус) — дальше листает он.
        function stop() { stopped = true; sync(); }

        new IntersectionObserver(function (entries) {
            visible = entries[0].isIntersecting;
            sync();
        }).observe(wrap);
        document.addEventListener("visibilitychange", sync);
        wrap.addEventListener("pointerenter", function () { hovered = true; sync(); });
        wrap.addEventListener("pointerleave", function () { hovered = false; sync(); });
        track.addEventListener("pointerdown", stop, { passive: true });
        track.addEventListener("wheel", stop, { passive: true });
        wrap.addEventListener("focusin", stop);
        if (prev) prev.addEventListener("click", stop);
        if (next) next.addEventListener("click", stop);
        sync();
    });
})();
