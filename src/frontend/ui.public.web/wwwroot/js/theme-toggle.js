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
            // ссылка готова к следующему клику (и как фоллбек, если fetch не дойдёт)
            link.href = link.href.replace(/theme=\w+/, "theme=" + (next === "light" ? "dark" : "light"));
            link.textContent = next === "light" ? "☾" : "☀";
        }

        if (document.startViewTransition &&
            !window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
            document.startViewTransition(apply);
        } else {
            apply();
        }

        // навигации не будет — подчистить позицию, сохранённую restore-scroll.js
        // по этому же клику (иначе она сработала бы на следующем заходе на страницу)
        try { sessionStorage.removeItem("kola-restore-scroll"); } catch (err) { /* не критично */ }

        fetch("/set-theme?theme=" + next, { redirect: "manual", keepalive: true });
    });
})();
