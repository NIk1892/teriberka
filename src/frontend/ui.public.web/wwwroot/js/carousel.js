// Стрелки каруселей (сейчас — галереи страниц мест). Прогрессивное улучшение:
// без скрипта кнопки скрыты, карусель полностью работает свайпом/колесом
// (scroll-snap). Обслуживает каждый .carousel-wrap с треком .carousel-track.
(function () {
    "use strict";

    document.querySelectorAll(".carousel-wrap").forEach(function (wrap) {
        var track = wrap.querySelector(".carousel-track");
        var prev = wrap.querySelector(".car-prev");
        var next = wrap.querySelector(".car-next");
        if (!track || !prev || !next) return;

        var smooth = window.matchMedia("(prefers-reduced-motion: reduce)").matches ? "auto" : "smooth";

        function step() {
            var item = track.firstElementChild;
            if (!item) return track.clientWidth;
            var gap = parseFloat(getComputedStyle(track).columnGap) || 0;
            return item.getBoundingClientRect().width + gap;
        }

        function update() {
            var max = track.scrollWidth - track.clientWidth - 1;
            prev.disabled = track.scrollLeft <= 1;
            next.disabled = track.scrollLeft >= max;
        }

        prev.addEventListener("click", function () {
            track.scrollBy({ left: -step(), behavior: smooth });
        });
        next.addEventListener("click", function () {
            track.scrollBy({ left: step(), behavior: smooth });
        });
        track.addEventListener("scroll", update, { passive: true });
        window.addEventListener("resize", update);

        wrap.classList.add("carousel-js");
        update();
    });
})();
