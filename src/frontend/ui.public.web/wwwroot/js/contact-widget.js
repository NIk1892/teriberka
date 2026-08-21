// Плавающая кнопка связи. Само меню — нативный <details>: открытие, закрытие и
// клик мимо работают без JavaScript (см. .contact-widget в app.css). Скрипт
// добавляет только то, чего details не умеет сам: Esc с возвратом фокуса на
// кнопку и сворачивание после выбора канала — ссылки уходят в новую вкладку, и
// при возврате виджет остался бы раскрытым, накрыв страницу оверлеем.
// prefers-reduced-motion здесь не проверяем: это функциональность, а не эффект —
// все анимации живут в CSS и гасятся там же.
(() => {
    const widget = document.querySelector("details.contact-widget");
    if (!widget) return;

    document.addEventListener("keydown", (e) => {
        if (e.key !== "Escape" || !widget.open) return;

        widget.open = false;
        widget.querySelector("summary")?.focus();
    });

    widget.addEventListener("click", (e) => {
        if (e.target.closest("a.contact-item")) widget.open = false;
    });
})();
