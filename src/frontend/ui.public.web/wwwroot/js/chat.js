// Чат с менеджером. Без JavaScript он тоже работает: пункт «Чат на сайте» — обычная
// ссылка ?chat=open, панель рендерит сервер, отправка идёт POST'ом с редиректом
// обратно (см. ChatPanel.razor). Скрипт убирает перезагрузки: открывает панель на
// месте, отправляет сообщение фоном и опрашивает /chat/poll на новые ответы.
//
// prefers-reduced-motion здесь не проверяется: это функциональность, а не эффект —
// анимации живут в CSS и гасятся там же.
(() => {
    "use strict";

    const panel = document.querySelector(".chat-panel");
    if (!panel) return;

    const root = document.documentElement;
    const form = panel.querySelector(".chat-form");
    const log = panel.querySelector(".chat-log");
    const input = panel.querySelector("#chat-text");
    const widget = document.querySelector("details.contact-widget");
    const status = panel.querySelector(".chat-status");
    const statusLabel = status?.querySelector(".chat-status-label");
    const responseTime = panel.querySelector(".chat-response-time");

    // Под этим классом CSS прячет ссылку «обновить переписку» — с поллингом она не нужна
    root.classList.add("js-chat");

    // Самый большой номер сообщения, которое уже на экране. Сервер отдаёт его в
    // data-after, когда рендерит историю сам.
    let after = Number(panel.dataset.after || 0);
    let timer = null;
    let failures = 0;

    const PERIOD_OPEN = 3000;
    const PERIOD_HIDDEN = 15000;
    const PERIOD_MAX = 60000;

    // Статус «✓ Доставлено менеджеру» меняется у сообщений, которые уже показаны, а опрос
    // отдаёт только сообщения новее курсора. Поэтому, пока есть недоставленные, опрос
    // начинается с первого из них и перечитывает хвост. Окно меньше страницы опроса
    // (ChatLimits.PageSize = 30): давнее недоставленное — например, при выключенной
    // доставке — не должно заслонять новые ответы.
    const STATUS_WINDOW = 20;

    const isOpen = () => panel.classList.contains("is-open");

    const period = () => {
        if (failures > 0) return Math.min(PERIOD_OPEN * 2 ** failures, PERIOD_MAX);
        return document.hidden ? PERIOD_HIDDEN : PERIOD_OPEN;
    };

    const cursor = () => {
        let from = after;

        log.querySelectorAll("li.from-you[data-o]:not(.is-delivered)").forEach((item) => {
            const ordinal = Number(item.dataset.o);
            if (ordinal > after - STATUS_WINDOW && ordinal - 1 < from) from = ordinal - 1;
        });

        return Math.max(0, from);
    };

    const create = (message) => {
        const item = document.createElement("li");
        item.className = "chat-msg " + (message.d === 1 ? "from-guide" : "from-you");

        const who = document.createElement("span");
        who.className = "chat-who";
        who.textContent = message.d === 1 ? log.dataset.guide : log.dataset.you;

        const text = document.createElement("span");
        text.className = "chat-text";
        // только textContent: текст приходит из чужого сообщения, innerHTML тут — дыра
        text.textContent = message.t || "";

        item.append(who, text);
        log.querySelector(".chat-empty")?.remove();
        log.append(item);
        log.scrollTop = log.scrollHeight;
        return item;
    };

    // Статус есть только у сообщений посетителя.
    const setState = (item, delivered) => {
        let state = item.querySelector(".chat-state");

        if (!state) {
            state = document.createElement("span");
            state.className = "chat-state";
            item.append(state);
        }

        item.classList.toggle("is-delivered", delivered);
        state.textContent = delivered ? log.dataset.delivered : log.dataset.sent;
    };

    // Сообщение из опроса: новое — дорисовать, уже показанное — обновить статус.
    const upsert = (message) => {
        let item = log.querySelector(`li[data-o="${Number(message.o)}"]`);

        if (!item && message.d !== 1) {
            // своё сообщение, нарисованное до ответа /chat/send: опрос мог его опередить
            item = [...log.querySelectorAll("li.from-you:not([data-o])")]
                .find((pending) => pending.querySelector(".chat-text")?.textContent === (message.t || ""));
        }

        if (!item) item = create(message);

        item.dataset.o = String(message.o);
        if (message.d !== 1) setState(item, Boolean(message.v));
        if (message.o > after) after = message.o;
    };

    const showWarning = (key) => {
        let warning = panel.querySelector(".chat-warn");

        if (!warning) {
            warning = document.createElement("p");
            warning.className = "chat-warn";
            warning.setAttribute("role", "alert");
            form.before(warning);
        }

        warning.textContent = panel.dataset[key] || panel.dataset.error;
    };

    const clearWarning = () => panel.querySelector(".chat-warn")?.remove();

    const updatePresence = (online) => {
        if (typeof online !== "boolean") return;

        status?.classList.toggle("is-online", online);
        status?.classList.toggle("is-offline", !online);
        const key = online ? "online" : "offline";
        const label = status?.dataset[key];
        const hint = responseTime?.dataset[key];

        // Update live text only when it changes, so polling does not repeat announcements.
        if (statusLabel && label && statusLabel.textContent !== label) statusLabel.textContent = label;
        if (responseTime && hint && responseTime.textContent.trim() !== hint) responseTime.textContent = hint;
    };

    const poll = async () => {
        try {
            const response = await fetch(`/chat/poll?after=${cursor()}`, {
                headers: { Accept: "application/json" },
            });

            if (!response.ok) throw new Error(String(response.status));

            const data = await response.json();
            failures = 0;

            (data.messages || []).forEach(upsert);

            updatePresence(data.online);
        } catch {
            // Сеть или сервис недоступны — молча ждём дольше, чат остаётся рабочим
            failures += 1;
        } finally {
            schedule();
        }
    };

    const schedule = () => {
        clearTimeout(timer);
        if (isOpen()) timer = setTimeout(poll, period());
    };

    // ---- капча первого сообщения ------------------------------------------------
    // Невидимая SmartCaptcha — та же, что у формы заявки (ключи и CSP общие). Сервер
    // требует её только у первого сообщения нового диалога (/chat/send в Program.cs):
    // спам 11–18.09.2026 слал бот без JavaScript, находивший форму чата в HTML.
    // Виджет Яндекса тяжёлый (~700 КБ), поэтому грузится, только когда панель открыли,
    // а диалога ещё нет. Слота нет — капча выключена ключами, скрипт работает как раньше.
    const captchaSlot = panel.querySelector(".chat-captcha");
    let hasSession = panel.dataset.session === "true";
    let captchaReady = null;   // Promise: captcha.js загружен и виджет отрисован
    let captchaId = null;
    let captchaUsed = false;   // токен одноразовый: перед следующим — reset
    let captchaWaiter = null;  // resolve ожидающего токен
    let verifying = false;     // идёт проверка — вторую отправку не начинаем

    const finishCaptcha = (token) => {
        const resolve = captchaWaiter;
        captchaWaiter = null;
        resolve?.(token);
    };

    const prepareCaptcha = () => {
        if (captchaReady) return captchaReady;

        captchaReady = new Promise((resolve, reject) => {
            const render = () => {
                const lang = (root.lang || "ru").slice(0, 2);
                captchaId = window.smartCaptcha.render(captchaSlot, {
                    sitekey: captchaSlot.dataset.sitekey,
                    invisible: true,
                    // виджет не знает китайского — для zh-версии сайта берём en
                    hl: lang === "ru" ? "ru" : "en",
                    // Бейдж спрятан, как у формы заявки: условия сервиса требуют тогда
                    // своего уведомления — оно стоит под формой чата (.chat-note).
                    hideShield: true,
                    callback: (token) => finishCaptcha(typeof token === "string" && token ? token : null),
                });
                // Задание закрыли, не решив, — отправки не будет. challenge-hidden
                // приходит и после успешного решения, поэтому с запасом ждём: токен
                // из callback успеет раньше, и тогда этот null уже никому не адресован.
                window.smartCaptcha.subscribe(captchaId, "challenge-hidden",
                    () => setTimeout(() => finishCaptcha(null), 2000));
                window.smartCaptcha.subscribe(captchaId, "network-error", () => finishCaptcha(null));
                resolve();
            };

            if (window.smartCaptcha) {
                render();
                return;
            }

            // На главной captcha.js мог уже запросить smart-captcha.js формы заявки —
            // второй раз тот же скрипт не грузим, ждём первый.
            const src = captchaSlot.dataset.src;
            let script = document.querySelector(`script[src="${src}"]`);

            if (script?.dataset.failed) {
                reject();
                return;
            }

            if (!script) {
                script = document.createElement("script");
                script.src = src;
                script.async = true;
                script.addEventListener("error", () => { script.dataset.failed = "1"; });
                document.head.append(script);
            }

            script.addEventListener("load", () => (window.smartCaptcha ? render() : reject()));
            script.addEventListener("error", () => reject());
        });

        // отказ разбирает captchaToken; без этого консоль ругалась бы на необработанный
        captchaReady.catch(() => {});
        return captchaReady;
    };

    // Токен капчи или null: задание закрыли, виджет заблокирован, сеть.
    const captchaToken = async () => {
        try {
            await prepareCaptcha();
        } catch {
            return null;
        }

        if (captchaUsed) window.smartCaptcha.reset(captchaId);
        captchaUsed = true;

        return new Promise((resolve) => {
            captchaWaiter = resolve;
            window.smartCaptcha.execute(captchaId);
        });
    };

    const addCaptcha = async (body) => {
        verifying = true;
        try {
            const token = await captchaToken();
            if (!token) return false;
            body.set("smart-token", token);
            return true;
        } finally {
            verifying = false;
        }
    };

    // Капчу не прошли — сообщение не ушло: убираем его из ленты и возвращаем текст в поле.
    const captchaFailed = (item, text) => {
        item.remove();
        if (!input.value) input.value = text;
        showWarning("captcha");
    };

    const warmCaptcha = () => {
        if (captchaSlot && !hasSession) prepareCaptcha();
    };

    const open = () => {
        panel.classList.add("is-open");
        if (widget) widget.open = false;
        input?.focus();
        warmCaptcha();
        poll();
    };

    const close = () => {
        panel.classList.remove("is-open");
        clearTimeout(timer);
    };

    // пункт «Чат на сайте» в меню связи: открываем на месте вместо перезагрузки
    document.addEventListener("click", (e) => {
        const item = e.target.closest("a.contact-chat");
        if (!item) return;

        e.preventDefault();
        open();
    });

    panel.querySelector(".chat-close")?.addEventListener("click", (e) => {
        e.preventDefault();
        close();
    });

    document.addEventListener("keydown", (e) => {
        if (e.key === "Escape" && isOpen()) close();
    });

    const post = (body) => fetch("/chat/send", {
        method: "POST",
        headers: { Accept: "application/json" },
        body,
    });

    input?.addEventListener("focus", warmCaptcha);

    form?.addEventListener("submit", async (e) => {
        e.preventDefault();
        if (verifying) return;

        const text = input.value.trim();
        if (!text) return;

        clearWarning();
        const body = new URLSearchParams(new FormData(form));

        // рисуем своё сообщение сразу: ждать ответа сервера ради эха незачем. Статуса у него
        // пока нет — «Отправлено» появится, только когда сервер сообщение принял.
        const item = create({ d: 0, t: text });
        input.value = "";

        try {
            // первое сообщение диалога — сразу с токеном, чтобы не ловить отказ сервера
            if (captchaSlot && !hasSession && !(await addCaptcha(body))) {
                captchaFailed(item, text);
                return;
            }

            let response = await post(body);

            // Сервер всё же потребовал капчу: cookie диалога устарела (переписку удалили
            // по сроку хранения) или токен отвергнут. Проходим и повторяем один раз.
            if (response.status === 403 && captchaSlot) {
                if (!(await addCaptcha(body))) {
                    captchaFailed(item, text);
                    return;
                }
                response = await post(body);
            }

            if (response.status === 403) {
                captchaFailed(item, text);
                return;
            }

            if (response.status === 429) {
                showWarning("tooMany");
                return;
            }

            if (!response.ok) {
                showWarning("error");
                return;
            }

            const data = await response.json();
            hasSession = true;

            // Опрос мог успеть раньше и уже проставить номер и статус — тогда не трогаем.
            if (data.ordinal > 0 && !item.dataset.o) {
                item.dataset.o = String(data.ordinal);
                setState(item, false);
                if (data.ordinal > after) after = data.ordinal;
            }

            failures = 0;
            schedule();
        } catch {
            showWarning("error");
        }
    });

    document.addEventListener("visibilitychange", schedule);

    // страница пришла уже с ?chat=open (переход без JS или перезагрузка после отправки)
    if (isOpen()) {
        log.scrollTop = log.scrollHeight;
        warmCaptcha();
        schedule();
    }
})();
