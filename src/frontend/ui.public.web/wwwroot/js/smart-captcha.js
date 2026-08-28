// Невидимая Яндекс SmartCaptcha на форме заявки. Подключается только при
// заданной паре ключей (разметка и скрипты — ApplicationForm.razor, CSP —
// Program.cs). Схема из документации invisible-captcha: submit перехватывается,
// execute() запускает проверку, токен приходит в callback — кладём его в hidden
// smart-token и отправляем форму по-настоящему; сервер проверяет токен в
// SubmitAsync. Без JavaScript токен не собрать — сервер честно откажет с
// подсказкой включить его (осознанная цена капчи, см. CLAUDE.md).
(function () {
    "use strict";

    var slot = document.querySelector(".captcha-slot");
    var form = document.querySelector(".apply-form");
    if (!slot || !form) return;

    var tokenInput = form.querySelector('input[name="smart-token"]');
    var widgetId = null;
    var solved = false;

    function init() {
        var lang = (document.documentElement.lang || "ru").slice(0, 2);
        widgetId = window.smartCaptcha.render(slot, {
            sitekey: slot.dataset.sitekey,
            invisible: true,
            // виджет умеет ru/en/be/kk/tt/uk/uz/tr — для zh-версии сайта берём en
            hl: lang === "ru" ? "ru" : "en",
            // бейдж «обработка данных» — слева: правый нижний угол занят
            // виджетом связи и кнопками прокрутки (прятать бейдж нельзя по
            // условиям сервиса — hideShield только вместе со своим уведомлением)
            shieldPosition: "bottom-left",
            callback: function (token) {
                if (typeof token !== "string" || token.length === 0) return;
                if (tokenInput) tokenInput.value = token;
                solved = true;
                // requestSubmit, а не submit(): нужен полный цикл события,
                // чтобы phone-intl.js собрал номер с кодом страны
                form.requestSubmit();
            },
        });

        // токен одноразовый и живёт 5 минут: протух до отправки — начинаем заново
        window.smartCaptcha.subscribe(widgetId, "token-expired", function () {
            solved = false;
            if (tokenInput) tokenInput.value = "";
            window.smartCaptcha.reset(widgetId);
        });
    }

    // captcha.js подключён defer'ом строкой выше нас, к нашему исполнению
    // window.smartCaptcha уже есть; страховка на случай медленной загрузки.
    if (window.smartCaptcha) init();
    else window.addEventListener("load", function () {
        if (window.smartCaptcha && widgetId === null) init();
    });

    // Обработчик на document (bubble): событие доходит сюда ПОСЛЕ валидации
    // form-ui.js, которая висит на самой форме, — невалидная форма до капчи
    // не добирается и токены зря не жгутся. Если виджет не поднялся (блокировщик,
    // сеть) — форма уходит без токена, отказ покажет сервер.
    document.addEventListener("submit", function (e) {
        if (e.target !== form || e.defaultPrevented || solved || widgetId === null) return;
        e.preventDefault();
        window.smartCaptcha.execute(widgetId);
    });
})();
