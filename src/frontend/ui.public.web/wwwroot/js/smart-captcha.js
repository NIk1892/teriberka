// Невидимая Яндекс SmartCaptcha на форме заявки. Подключается только при
// заданной паре ключей (разметка — ApplicationForm.razor, CSP — Program.cs).
// Схема из документации invisible-captcha: submit перехватывается, execute()
// запускает проверку, токен приходит в callback — кладём его в hidden
// smart-token и отправляем форму по-настоящему; сервер проверяет токен в
// SubmitAsync. Без JavaScript токен не собрать — сервер честно откажет с
// подсказкой включить его (осознанная цена капчи, см. CLAUDE.md).
//
// Сам виджет Яндекса (captcha.js и следом ~700 КБ его скриптов и iframe'ов)
// подключается НЕ вместе со страницей, а когда форма подходит к экрану или
// посетитель её трогает (16.09.2026): форма стоит в самом низу главной, а
// капча грузилась у каждого посетителя сразу и была самым тяжёлым ресурсом
// страницы — PageSpeed считал её в LCP и в блокировку главного потока.
// Адрес captcha.js едет data-src слота (единственный источник — C#, тот же
// хост, что в CSP). Нажали «отправить» раньше, чем виджет поднялся, —
// отправка ждёт его загрузки и уходит сама.
(function () {
    "use strict";

    var slot = document.querySelector(".captcha-slot");
    var form = document.querySelector(".apply-form");
    if (!slot || !form) return;

    var tokenInput = form.querySelector('input[name="smart-token"]');
    var widgetId = null;
    var solved = false;
    var loading = false;       // captcha.js уже запрошен
    var failed = false;        // captcha.js не загрузился (блокировщик, сеть) — форма уходит без токена
    var pendingSubmit = false; // посетитель нажал «отправить», пока виджет ещё грузился

    function init() {
        if (widgetId !== null || !window.smartCaptcha) return;
        var lang = (document.documentElement.lang || "ru").slice(0, 2);
        widgetId = window.smartCaptcha.render(slot, {
            sitekey: slot.dataset.sitekey,
            invisible: true,
            // виджет умеет ru/en/be/kk/tt/uk/uz/tr — для zh-версии сайта берём en
            hl: lang === "ru" ? "ru" : "en",
            // Бейдж «обработка данных» скрыт (владелец, 28.08.2026): в Safari
            // его крестик не закрывал плашку, а состояние не запоминалось.
            // Условия сервиса разрешают hideShield только вместе с собственным
            // уведомлением — оно стоит под формой (.captcha-note в
            // ApplicationForm.razor, ссылка на yandex.ru/legal/smartcaptcha_notice).
            hideShield: true,
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

        // форму отправили, пока виджет грузился, — проверка стартует сейчас
        if (pendingSubmit) {
            pendingSubmit = false;
            window.smartCaptcha.execute(widgetId);
        }
    }

    function load() {
        if (loading) return;
        loading = true;
        if (window.smartCaptcha) { init(); return; }
        var src = slot.dataset.src;
        if (!src) { failed = true; return; }
        var s = document.createElement("script");
        s.src = src;
        s.async = true;
        s.onload = init;
        s.onerror = function () {
            // виджет не поднялся — форма уходит без токена, отказ покажет сервер
            // (то же поведение, что было у блокировщиков до ленивой загрузки)
            failed = true;
            if (pendingSubmit) {
                pendingSubmit = false;
                solved = true;
                form.requestSubmit();
            }
        };
        document.head.appendChild(s);
    }

    // Форма подходит к экрану (запас ~1000px — на слабом мобильном интернете
    // виджету нужно несколько секунд) или посетитель взялся за неё — грузим.
    if ("IntersectionObserver" in window) {
        var io = new IntersectionObserver(function (entries) {
            if (!entries.some(function (en) { return en.isIntersecting; })) return;
            io.disconnect();
            load();
        }, { rootMargin: "1000px 0px" });
        io.observe(form);
    } else {
        load();
    }
    ["focusin", "pointerdown", "touchstart"].forEach(function (ev) {
        form.addEventListener(ev, load, { once: true, passive: true });
    });

    // Отправка без перезагрузки не удалась (apply-submit.js): токен уже потрачен
    // сервером, следующая попытка должна пройти капчу заново.
    form.addEventListener("apply:failed", function () {
        solved = false;
        if (tokenInput) tokenInput.value = "";
        if (widgetId !== null) window.smartCaptcha.reset(widgetId);
    });

    // Обработчик на document (bubble): событие доходит сюда ПОСЛЕ валидации
    // form-ui.js, которая висит на самой форме, — невалидная форма до капчи
    // не добирается и токены зря не жгутся. Если виджет не поднялся (блокировщик,
    // сеть) — форма уходит без токена, отказ покажет сервер.
    document.addEventListener("submit", function (e) {
        if (e.target !== form || e.defaultPrevented || solved || failed) return;
        e.preventDefault();
        if (widgetId === null) {
            // виджет ещё грузится (или его не запрашивали — например, отправили
            // с клавиатуры, не трогая форму): дождёмся и проверим
            pendingSubmit = true;
            load();
            return;
        }
        window.smartCaptcha.execute(widgetId);
    });
})();
