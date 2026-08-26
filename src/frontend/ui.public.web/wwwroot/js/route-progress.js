// Маршрут дня (.itinerary-route): линия-прогресс «рисуется» по мере прокрутки,
// точки-номера загораются, когда линия до них доходит, а по нити едет серый
// микроавтобус (инлайн-SVG: окна — вырезы fill-rule evenodd, поэтому силуэт
// работает на любом фоне). Скрипт пишет только CSS-переменные --route-p /
// --route-py и классы .lit / .on; сами трансформы применяет app.css по html.js-route.
// Без JS остаётся CSS-фоллбек (scroll-driven animation рисует линию), при
// prefers-reduced-motion скрипт не запускается вовсе. Расчёт идёт только пока
// блок виден (IntersectionObserver) — по тому же образцу, что parallax.js.
(function () {
    "use strict";

    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;

    var route = document.querySelector(".itinerary-route");
    if (!route) return;

    document.documentElement.classList.add("js-route");

    var markers = Array.prototype.slice.call(route.querySelectorAll(".route-stop-marker"));
    var stops = markers.map(function (m) { return m.parentElement; });

    var dot = document.createElement("span");
    dot.className = "route-van";
    dot.setAttribute("aria-hidden", "true");
    // Силуэт микроавтобуса (вид сверху, капотом вниз — по ходу движения, как
    // машинка в навигаторе; вид сбоку выглядел едущим боком — владелец, 25.08.2026).
    // Окна — вырезы evenodd, зеркала — отдельные штрихи по бокам у лобового.
    // innerHTML безопасен: строка статическая, пользовательских данных нет.
    dot.innerHTML =
        '<svg viewBox="0 0 24 44" role="presentation">' +
        '<path fill="currentColor" fill-rule="evenodd" d="M4 6 Q4 3 7 3 L17 3 ' +
        'Q20 3 20 6 L20 36 Q20 41 15 41 L9 41 Q4 41 4 36 Z ' +
        'M6.5 7 L17.5 7 L17.5 10 L6.5 10 Z ' +
        'M6 29.5 L18 29.5 L16.8 33.5 Q12 35.8 7.2 33.5 Z"/>' +
        '<path fill="currentColor" d="M1.5 29 L4 29.7 L4 31.7 L1.5 31 Z ' +
        'M22.5 29 L20 29.7 L20 31.7 L22.5 31 Z"/>' +
        '</svg>';
    route.appendChild(dot);

    // Геометрия линии — те же отступы, что у .itinerary-route::before в app.css.
    var LINE_TOP = 14, LINE_BOTTOM = 40;
    var boxH = 1, thresholds = [];

    function measure() {
        boxH = Math.max(1, route.offsetHeight - LINE_TOP - LINE_BOTTOM);
        thresholds = markers.map(function (m) {
            // offsetTop игнорирует transform reveal-каскада effects.js, поэтому
            // позиции меряются по layout, а не по текущему положению анимации.
            return m.parentElement.offsetTop + m.offsetTop + m.offsetHeight / 2 - LINE_TOP;
        });
    }

    var cur = 0, lastCur = 0, visible = false, running = false;

    function frame() {
        if (!visible) { running = false; return; }
        // «фокусная» линия чтения — чуть выше середины экрана
        var focus = window.innerHeight * 0.42;
        var target = focus - route.getBoundingClientRect().top - LINE_TOP;
        if (target < 0) target = 0;
        if (target > boxH) target = boxH;
        cur += (target - cur) * 0.14; // инерция: линия «догоняет» прокрутку
        if (Math.abs(target - cur) < 0.5) cur = target;
        route.style.setProperty("--route-p", (cur / boxH).toFixed(4));
        route.style.setProperty("--route-py", cur.toFixed(1) + "px");
        dot.classList.toggle("on", cur > 6 && cur < boxH - 2);
        // прокрутка вверх — разворот «капотом вверх», чтобы не ехал задом
        // (порог гасит дрожание около нуля; на месте держим последнее направление)
        if (cur - lastCur > 0.3) dot.classList.remove("rev");
        else if (cur - lastCur < -0.3) dot.classList.add("rev");
        lastCur = cur;
        for (var i = 0; i < markers.length; i++) {
            markers[i].classList.toggle("lit", cur >= thresholds[i]);
            // фото остановки плавно наезжает, пока машинка подъезжает: близость
            // 0..1 в радиусе 340px, квадрат — чтобы зум нарастал к самому пункту
            var k = 1 - Math.min(1, Math.abs(thresholds[i] - cur) / 340);
            stops[i].style.setProperty("--stop-zoom", (1 + 0.07 * k * k).toFixed(4));
        }
        requestAnimationFrame(frame);
    }

    function wake() {
        if (!running) {
            running = true;
            requestAnimationFrame(frame);
        }
    }

    var io = new IntersectionObserver(function (entries) {
        visible = entries[0].isIntersecting;
        wake();
    }, { rootMargin: "200px 0px" });
    io.observe(route);

    measure();
    // высота меняется от переносов текста и смены раскладки (шахматка ↔ колонка)
    if ("ResizeObserver" in window) {
        new ResizeObserver(measure).observe(route);
    } else {
        window.addEventListener("resize", measure);
    }
    window.addEventListener("load", measure);
}());
