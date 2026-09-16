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

    const poll = async () => {
        try {
            const response = await fetch(`/chat/poll?after=${cursor()}`, {
                headers: { Accept: "application/json" },
            });

            if (!response.ok) throw new Error(String(response.status));

            const data = await response.json();
            failures = 0;

            (data.messages || []).forEach(upsert);

            panel.querySelector(".chat-status")?.classList.toggle("is-online", data.online);
            panel.querySelector(".chat-status")?.classList.toggle("is-offline", !data.online);
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

    const open = () => {
        panel.classList.add("is-open");
        if (widget) widget.open = false;
        input?.focus();
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

    form?.addEventListener("submit", async (e) => {
        e.preventDefault();

        const text = input.value.trim();
        if (!text) return;

        clearWarning();
        const body = new URLSearchParams(new FormData(form));

        // рисуем своё сообщение сразу: ждать ответа сервера ради эха незачем. Статуса у него
        // пока нет — «Отправлено» появится, только когда сервер сообщение принял.
        const item = create({ d: 0, t: text });
        input.value = "";

        try {
            const response = await fetch("/chat/send", {
                method: "POST",
                headers: { Accept: "application/json" },
                body,
            });

            if (response.status === 429) {
                showWarning("tooMany");
                return;
            }

            if (!response.ok) {
                showWarning("error");
                return;
            }

            const data = await response.json();

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
        schedule();
    }
})();
