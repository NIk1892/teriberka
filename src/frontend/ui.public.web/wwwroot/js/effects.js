// «Дорогие» эффекты сайта: инерционная прокрутка (вендоренный Lenis из
// js/vendor/lenis.min.js), параллакс-слои по data-speed (как у andyhardy.co:
// элемент отстаёт от прокрутки на долю speed) и пружинящие reveal-каскады.
// Скрипт вешает html.js-fx — только под этим классом CSS прячет элементы
// перед появлением, поэтому без JS страница остаётся полностью видимой.
// При prefers-reduced-motion не запускается вовсе; если Lenis не загрузился,
// остальные эффекты работают на нативной прокрутке.
(function () {
    "use strict";

    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;

    document.documentElement.classList.add("js-fx");

    // ---- инерционная прокрутка -------------------------------------------
    // anchors: true — якорные ссылки (#apply, #top…) едут плавно через Lenis.
    // На тач-устройствах Lenis по умолчанию не трогает нативный скролл.
    // Своё смещение под шапку здесь НЕ задаём: у секций уже есть scroll-margin-top
    // в app.css, и offset Lenis складывался бы с ним — секция уезжала вдвое ниже.
    var lenis = null;
    try {
        if (typeof Lenis === "function") {
            lenis = new Lenis({ autoRaf: true, anchors: true });
        }
    } catch (e) { /* без Lenis остаётся нативная прокрутка */ }


    // ---- параллакс-слои по data-speed ------------------------------------
    // translate = смещение элемента от начала страницы * speed: положительный
    // speed — элемент отстаёт (читается дальше), отрицательный — обгоняет.
    // Считаем только пока элемент около вьюпорта (IntersectionObserver).
    var layers = Array.prototype.slice.call(document.querySelectorAll("[data-speed]"))
        .map(function (el) {
            return { el: el, speed: parseFloat(el.dataset.speed) || 0, visible: false, base: 0 };
        });

    function measure() {
        layers.forEach(function (l) {
            l.el.style.transform = "";
            l.base = l.el.getBoundingClientRect().top + window.scrollY;
        });
    }

    function applyLayers() {
        var sy = window.scrollY, vh = window.innerHeight;
        for (var i = 0; i < layers.length; i++) {
            var l = layers[i];
            if (!l.visible) continue;
            // насколько элемент «проехал» от своей исходной позиции
            var passed = sy - (l.base - vh > 0 ? l.base - vh : 0);
            if (passed < 0) passed = 0;
            var y = passed * l.speed;
            l.el.style.transform = "translate3d(0," + y.toFixed(1) + "px,0)";
        }
    }

    // Слои — только на широких экранах: в мобильной вёрстке hero складывается
    // в колонку и отстающий слой наезжал бы на следующую секцию (у andyhardy
    // hero-параллакс тоже desktop-only).
    if (layers.length && window.matchMedia("(min-width: 900px)").matches) {
        measure();
        var io = new IntersectionObserver(function (entries) {
            entries.forEach(function (en) {
                layers.forEach(function (l) {
                    if (l.el === en.target) l.visible = en.isIntersecting;
                });
            });
            applyLayers();
        }, { rootMargin: "120px 0px" });
        layers.forEach(function (l) { io.observe(l.el); });
        window.addEventListener("scroll", applyLayers, { passive: true });
        window.addEventListener("resize", function () { measure(); applyLayers(); }, { passive: true });
    }

    // ---- морф фото места между страницами (View Transitions) ------------
    // Одинаковый view-transition-name на карточке главной и на странице места:
    // при переходе фото «перелетает» из карточки в шапку места и обратно.
    // Ставится через CSSOM (инлайновый style="" запрещён CSP, el.style — нет).
    if (CSS.supports("view-transition-name: none")) {
        document.querySelectorAll(".place-card").forEach(function (a) {
            var m = (a.getAttribute("href") || "").match(/\/place\/([A-Za-z0-9-]+)/);
            var img = a.querySelector(".place-photo img");
            if (m && img) img.style.viewTransitionName = "place-" + m[1];
        });
        var detailImg = document.querySelector(".detail-photo img");
        var m2 = location.pathname.match(/\/place\/([A-Za-z0-9-]+)/);
        if (detailImg && m2) detailImg.style.viewTransitionName = "place-" + m2[1];
    }

    // ---- пружинящие reveal-каскады ---------------------------------------
    // Тем же селекторам, что поднимает CSS card-rise (под js-fx он выключен),
    // плюс заголовкам секций JS даёт каскад: transition-delay по индексу
    // среди соседей-ревилов, чтобы карточки выезжали одна за другой.
    var revealTargets = document.querySelectorAll(
        ".section-head, .place-card, .route-stop, .season-card, " +
        ".included-grid li, .why-grid li, .guide-card, .guide-note"
    );

    // После смены языка страница перезагружается, а restore-scroll.js (он
    // подключён ПОЗЖЕ и потребляет ключ после нас — здесь только подглядываем)
    // вернёт прокрутку на место. Блокам, которые окажутся на экране, каскад не
    // даём: повторное «выезжание» уже прочитанного дёргало страницу.
    var restoredY = -1;
    try {
        var savedRaw = sessionStorage.getItem("kola-restore-scroll");
        if (savedRaw) {
            var saved = JSON.parse(savedRaw);
            if (saved.path === location.pathname) restoredY = saved.y;
        }
    } catch (e) { /* нет доступа к sessionStorage — просто без пропуска */ }

    var ro = new IntersectionObserver(function (entries, obs) {
        entries.forEach(function (en) {
            if (!en.isIntersecting) return;
            var el = en.target;
            el.classList.add("appear");
            obs.unobserve(el);
            // после появления снять классы: элемент возвращает свои transition
            // (например, быстрый hover-подъём карточек вместо reveal-овских 1s)
            var done = function (e) {
                // transitionend всплывает от детей — ждём именно свой transform
                if (e && (e.target !== el || e.propertyName !== "transform")) return;
                el.classList.remove("reveal", "appear");
                el.style.transitionDelay = "";
                el.removeEventListener("transitionend", done);
            };
            el.addEventListener("transitionend", done);
            setTimeout(done, 1800); // страховка, если transitionend не пришёл
        });
    // срабатываем за 40px ДО входа блока в экран: анимация уже идёт, когда
    // блок показывается, — нет ощущения запаздывания при прокрутке
    }, { threshold: 0, rootMargin: "0px 0px 40px 0px" });

    Array.prototype.forEach.call(revealTargets, function (el) {
        // Внутри горизонтальной карусели (места на телефоне) каскад не даём: кадр
        // «появлялся» в момент листания и выезжал снизу, а у фото-полосы такого нет —
        // все карусели должны листаться одинаково. Тот же трек на десктопе — обычная
        // сетка без прокрутки, там каскад остаётся.
        var track = el.closest(".carousel-track");
        if (track && track.scrollWidth > track.clientWidth + 1) return;
        // элемент попадёт в восстановленный вьюпорт — оставить видимым без анимации
        // (запас 120px — на сдвиги вёрстки от шрифтов/картинок после загрузки)
        if (restoredY >= 0 &&
            el.getBoundingClientRect().top + window.scrollY < restoredY + window.innerHeight + 120) return;
        el.classList.add("reveal");
        // каскад: задержка по номеру среди соседних ревил-элементов
        var i = 0, sib = el;
        while ((sib = sib.previousElementSibling) && sib.classList.contains("reveal")) i++;
        el.style.transitionDelay = Math.min(i * 60, 300) + "ms";
        ro.observe(el);
    });
}());
