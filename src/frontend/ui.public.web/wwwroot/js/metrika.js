// Яндекс.Метрика (счётчик заведён владельцем 31.08.2026). Официальный сниппет
// вынесен в отдельный файл: CSP запрещает inline-скрипты (script-src 'self' плюс
// хост Метрики), поэтому вместо <script> с кодом в разметке подключается этот
// файл, а номер счётчика едет data-атрибутом — тот же приём, что у chat.js и
// smart-captcha.js. Рендерится он только при заданном YANDEX_METRIKA_ID (см.
// MetrikaService и YandexMetrika.razor).
(function () {
    "use strict";

    var tag = document.currentScript || document.querySelector("script[data-metrika]");
    var counter = tag && tag.getAttribute("data-metrika");
    if (!counter) return;

    // Очередь вызовов до загрузки tag.js — как в сниппете из документации:
    // ym(...) можно звать сразу, реальная библиотека разберёт накопленное.
    var ym = window.ym = window.ym || function () {
        (window.ym.a = window.ym.a || []).push(arguments);
    };
    ym.l = +new Date();

    var src = "https://mc.yandex.ru/metrika/tag.js?id=" + counter;
    // страховка от двойного подключения (второй счётчик считал бы визиты дважды)
    for (var i = 0; i < document.scripts.length; i++) {
        if (document.scripts[i].src === src) return;
    }

    var s = document.createElement("script");
    s.async = true;
    s.src = src;
    document.head.appendChild(s);

    ym(counter, "init", {
        // сайт рендерится на сервере (static SSR) — Метрика ждёт этот флаг
        ssr: true,
        clickmap: true,            // тепловая карта кликов
        trackLinks: true,          // клики по внешним ссылкам (Telegram, MAX, tel:)
        accurateTrackBounce: true, // визит дольше 15 секунд — не отказ
        webvisor: true,            // запись сессий: видно, где бросают форму
        referrer: document.referrer,
        url: location.href
        // ecommerce из сниппета намеренно не переносим: магазина нет, dataLayer
        // на сайте никто не наполняет — параметр создавал бы видимость данных
    });
})();
