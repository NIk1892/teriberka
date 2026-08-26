// Смена темы без перезагрузки. Ссылка /set-theme остаётся рабочим фоллбеком
// (без JavaScript — прежний рефреш с восстановлением прокрутки), а со скриптом
// тема применяется на месте: data-theme на <html>, meta theme-color и иконка
// ссылки; cookie ставит тот же endpoint фоновым fetch — сервер остаётся
// единственным местом, где задаются атрибуты cookie. Смена обёрнута в
// view transition (мягкий кроссфейд), при prefers-reduced-motion — мгновенно.
(function () {
    "use strict";

    // те же цвета, что рендерит App.razor в <meta name="theme-color">
    var COLORS = { dark: "#0a0f1d", light: "#f2f6fc" };

    document.addEventListener("click", function (e) {
        var link = e.target.closest('a[href^="/set-theme"]');
        if (!link) return;
        e.preventDefault();

        var next = document.documentElement.dataset.theme === "light" ? "dark" : "light";

        function apply() {
            document.documentElement.dataset.theme = next;
            var meta = document.querySelector('meta[name="theme-color"]');
            if (meta) meta.setAttribute("content", COLORS[next]);
            // Ссылка готова к следующему клику (и как фоллбек, если fetch не дойдёт).
            // Именно setAttribute с относительным путём: присвоение link.href пишет
            // в атрибут абсолютный URL, и селектор a[href^="/set-theme"] (он сравнивает
            // атрибут, а не свойство) перестаёт его находить — второй клик уходил
            // в полную навигацию и сбрасывал прокрутку наверх.
            var href = link.getAttribute("href") || "";
            link.setAttribute("href", href.replace(/theme=\w+/, "theme=" + (next === "light" ? "dark" : "light")));
            link.textContent = next === "light" ? "☾" : "☀";
        }

        if (document.startViewTransition &&
            !window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
            document.startViewTransition(apply);
        } else {
            apply();
        }

        // Страховка позиции: смена темы не должна дёргать страницу наверх.
        // Полсекунды следим за прокруткой и возвращаем её, если она прыгнула
        // (порог 40px не мешает обычному скроллу пользователя сразу после клика).
        var keepY = window.scrollY, frames = 0;
        (function pin() {
            if (Math.abs(window.scrollY - keepY) > 40) window.scrollTo(0, keepY);
            if (++frames < 30) requestAnimationFrame(pin);
        })();

        // навигации не будет — подчистить позицию, сохранённую restore-scroll.js
        // по этому же клику (иначе она сработала бы на следующем заходе на страницу)
        try { sessionStorage.removeItem("kola-restore-scroll"); } catch (err) { /* не критично */ }

        fetch("/set-theme?theme=" + next, { redirect: "manual", keepalive: true });
    });
})();
