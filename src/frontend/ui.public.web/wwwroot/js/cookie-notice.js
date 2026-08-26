// Уведомление о cookie: с работающим JavaScript кнопка «Хорошо» не перезагружает
// страницу — плашка уезжает вниз, а cookie ставит фоновой fetch на тот же
// endpoint /accept-cookies (она HttpOnly, из скрипта её не задать). Без JS
// остаётся обычная ссылка с редиректом на текущую страницу — плашка после
// перезагрузки уже не рендерится, решение принимает сервер.
(function () {
    "use strict";

    var notice = document.querySelector(".cookie-notice");
    if (!notice) return;

    var accept = notice.querySelector(".cookie-accept");
    if (!accept) return;

    accept.addEventListener("click", function (e) {
        e.preventDefault();
        notice.classList.add("is-gone");
        // плашка снимается после ухода вниз; при reduced-motion анимации нет —
        // тогда снимаем сразу
        var delay = window.matchMedia("(prefers-reduced-motion: reduce)").matches ? 0 : 350;
        setTimeout(function () { notice.remove(); }, delay);
        fetch("/accept-cookies", { redirect: "manual", keepalive: true });
    });
}());
