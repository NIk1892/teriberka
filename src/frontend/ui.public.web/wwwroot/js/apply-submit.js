// Отправка формы заявки без перезагрузки страницы (владелец, 16.09.2026: после
// редиректа страница «дёргалась» — прыгала наверх или ехала к карточке «спасибо»).
//
// Форма уходит fetch'ем на тот же адрес и тем же телом, что и обычный submit, поэтому
// серверная обработка не дублируется — валидация, капча и отправка остаются в
// ApplicationForm.SubmitAsync. Ответы сервера:
//  - редирект (на /?sent=true#apply) — заявка принята: на месте формы появляется
//    та же карточка «спасибо» из <template>, экран остаётся где был;
//  - 200 с разметкой — сервер вернул форму с ошибками: подсказки переносятся
//    к полям через form-ui.js, общая ошибка — над кнопкой;
//  - 429, сбой сети и прочее — общая ошибка текстом из data-* формы.
// Без JavaScript работает прежний путь: POST → редирект на /?sent=true#apply.
(function () {
    "use strict";

    var form = document.querySelector(".apply-form");
    var template = document.querySelector("template.apply-sent");
    var card = form && form.closest(".apply-card");
    if (!form || !template || !card || !window.fetch) return;

    var button = form.querySelector('button[type="submit"]');
    var buttonText = button ? button.textContent : "";
    var sending = false;

    // На window и в bubble-фазе: сюда событие доходит последним — после валидации
    // form-ui.js (на форме) и капчи smart-captcha.js (на document). Кто-то из них
    // остановил отправку — defaultPrevented уже выставлен, и мы не вмешиваемся.
    window.addEventListener("submit", function (e) {
        if (e.target !== form || e.defaultPrevented) return;
        e.preventDefault();
        if (sending) return;

        // Тело собираем сейчас: phone-intl.js уже подставил номер с кодом страны
        // и через 60 мс вернёт полю национальный вид.
        send(new URLSearchParams(new FormData(form)));
    });

    function send(body) {
        sending = true;
        setBusy(true);
        clearGeneralError();

        fetch(form.action, {
            method: "POST",
            body: body,
            credentials: "same-origin",
            // редирект — это и есть «заявка принята»; ходить за главной не нужно
            redirect: "manual",
        })
            .then(function (response) {
                if (response.type === "opaqueredirect" || (response.status >= 300 && response.status < 400)) {
                    showSent();
                    return;
                }
                if (response.status === 429)
                    return fail([], form.dataset.msgTooMany);
                if (!response.ok)
                    return fail([], form.dataset.msgError);
                return response.text().then(showServerErrors);
            })
            .catch(function () {
                fail([], form.dataset.msgError);
            });
    }

    // Сервер отрисовал форму заново — берём из неё подсказки под полями и общую ошибку.
    function showServerErrors(html) {
        var doc = new DOMParser().parseFromString(html, "text/html");
        var errors = [];

        doc.querySelectorAll(".apply-form [data-field]").forEach(function (field) {
            var hint = field.querySelector(".field-hint-error");
            if (hint)
                errors.push({ field: field.dataset.field, message: hint.textContent });
        });

        var general = doc.querySelector(".apply-card .form-errors");
        var message = general ? general.textContent : "";

        // ни подсказок, ни ошибки — ответ непонятный, но заявка точно не ушла
        if (errors.length === 0 && !message)
            message = form.dataset.msgError;

        fail(errors, message);
    }

    function fail(errors, message) {
        sending = false;
        setBusy(false);

        if (message)
            showGeneralError(message);

        // подсказки рисует form-ui.js — тем же видом и с тем же подкатом к первому полю
        if (errors.length)
            form.dispatchEvent(new CustomEvent("apply:errors", { detail: errors }));

        // токен капчи одноразовый: следующая попытка должна получить новый
        form.dispatchEvent(new CustomEvent("apply:failed"));
    }

    function showSent() {
        var head = document.querySelector(".apply-head");

        // Карточка встаёт туда, где была видимая часть формы: если верх формы на экране —
        // ровно на её место, если уже уехал вверх — под шапку сайта (отступ тот же,
        // что у якоря #apply — scroll-margin-top карточки).
        var formTop = form.getBoundingClientRect().top;

        var sent = template.content.firstElementChild.cloneNode(true);
        card.replaceChildren(sent);
        // как в серверной ветке Sent: над карточкой «спасибо» шапки формы нет
        if (head)
            head.remove();

        var margin = parseFloat(getComputedStyle(sent).scrollMarginTop) || 0;
        var shift = sent.getBoundingClientRect().top - Math.max(formTop, margin);

        // Мгновенно и в том же кадре, что и замена: содержимое выше экрана схлопнулось,
        // а посетитель видит карточку на месте формы, без проезда страницы.
        if (Math.abs(shift) > 1)
            window.scrollBy({ top: shift, behavior: "instant" });

        sent.setAttribute("tabindex", "-1");
        sent.focus({ preventScroll: true });

        if (sent.animate && !window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
            sent.animate(
                [
                    { opacity: 0, transform: "translateY(12px) scale(0.98)" },
                    { opacity: 1, transform: "none" },
                ],
                { duration: 450, easing: "cubic-bezier(0.2, 0.8, 0.2, 1)" }
            );
        }

        trackSent();
    }

    // Цель в Метрике планировалась на визит /?sent=true (CLAUDE.md, «Аналитика»):
    // перехода теперь нет, поэтому тот же адрес уходит виртуальным хитом.
    function trackSent() {
        var tag = document.querySelector("script[data-metrika]");
        var counter = tag && Number(tag.getAttribute("data-metrika"));
        if (!counter || typeof window.ym !== "function") return;

        window.ym(counter, "hit", location.origin + "/?sent=true", {
            title: document.title,
            referer: location.href,
        });
    }

    function setBusy(busy) {
        form.setAttribute("aria-busy", busy ? "true" : "false");
        if (!button) return;
        button.disabled = busy;
        button.textContent = busy && form.dataset.msgSending ? form.dataset.msgSending : buttonText;
    }

    // Общая ошибка — над кнопкой, а не над формой, как у серверной отрисовки:
    // посетитель только что нажал кнопку и смотрит на неё.
    function showGeneralError(message) {
        var box = document.createElement("p");
        box.className = "form-errors apply-submit-error";
        box.setAttribute("role", "alert");
        box.textContent = message;

        var row = form.querySelector(".form-submit");
        form.insertBefore(box, row);
    }

    function clearGeneralError() {
        card.querySelectorAll(".form-errors").forEach(function (el) {
            el.remove();
        });
    }
})();
