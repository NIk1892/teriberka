// Стрелки карусели отзывов. Прогрессивное улучшение: без скрипта кнопки скрыты,
// карусель полностью работает свайпом/колесом (scroll-snap).
(function () {
    "use strict";

    var wrap = document.querySelector(".carousel-wrap");
    if (!wrap) return;

    var track = wrap.querySelector(".reviews-carousel");
    var prev = wrap.querySelector(".car-prev");
    var next = wrap.querySelector(".car-next");
    if (!track || !prev || !next) return;

    var smooth = window.matchMedia("(prefers-reduced-motion: reduce)").matches ? "auto" : "smooth";

    function step() {
        var card = track.querySelector(".review-card");
        return card ? card.getBoundingClientRect().width + 16 : track.clientWidth;
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
})();
