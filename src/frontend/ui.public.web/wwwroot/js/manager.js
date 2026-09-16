// Страница заявок (/manager) без перезагрузок: вкладки, страницы списка и отметки.
//
// Вкладки и «Новее / Старше» — обычные ссылки: скрипт запрашивает тот же адрес fetch'ем,
// берёт из ответа только блок .mgr-main (вкладки со счётчиками, список, пагинатор) и
// подменяет его на месте, адрес в строке меняется через history — «Назад» работает.
// Разметку по-прежнему рисует сервер, дублировать её в скрипте не нужно.
//
// Отметки — формы на /manager/process: тело то же, что у формы без скрипта, но через fetch;
// меняются только класс карточки, подпись статуса и счётчики вкладок.
//
// Без JavaScript всё работает обычными переходами и POST с возвратом к карточке.
(function () {
    "use strict";

    var main = document.querySelector(".mgr-main");
    if (!main || !window.fetch) return;

    var pending = null;

    // Делегирование, а не обработчики на элементах: после подмены списка элементы новые.
    document.addEventListener("submit", function (e) {
        var form = e.target.closest && e.target.closest(".mgr-process");
        if (!form) return;
        e.preventDefault();
        send(form, e.submitter);
    });

    document.addEventListener("click", function (e) {
        var link = e.target.closest && e.target.closest(".mgr-tabs a, .mgr-pager a");
        // новая вкладка браузера (Ctrl/Cmd/Shift, средняя кнопка) — как обычная ссылка
        if (!link || e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
        e.preventDefault();

        // вкладку подсвечиваем сразу — отклик на нажатие не ждёт ответа сервера
        if (link.closest(".mgr-tabs")) {
            main.querySelectorAll(".mgr-tabs a").forEach(function (a) {
                a.classList.toggle("is-active", a === link);
            });
        }

        navigate(link.href, true, !!link.closest(".mgr-pager"));
    });

    window.addEventListener("popstate", function () {
        navigate(location.href, false, false);
    });

    function navigate(url, push, toTop) {
        if (pending) pending.abort();
        var controller = new AbortController();
        pending = controller;
        main.classList.add("is-loading");
        main.setAttribute("aria-busy", "true");

        fetch(url, { credentials: "same-origin", signal: controller.signal })
            .then(function (response) {
                // сессия истекла или сменили пароль — сервер увёл на вход
                if (response.redirected && new URL(response.url).pathname !== location.pathname) {
                    location.href = response.url;
                    return null;
                }
                if (!response.ok) throw new Error(String(response.status));
                return response.text();
            })
            .then(function (html) {
                if (html === null) return;
                var doc = new DOMParser().parseFromString(html, "text/html");
                var fresh = doc.querySelector(".mgr-main");
                if (!fresh) throw new Error("no .mgr-main");

                main.replaceChildren.apply(main, Array.prototype.slice.call(fresh.childNodes));
                if (doc.title) document.title = doc.title;
                if (push) history.pushState(null, "", url);
                // «Старше» нажимают внизу списка — новую страницу показываем с начала
                if (toTop) window.scrollTo({ top: 0, behavior: "instant" });
            })
            .catch(function (err) {
                if (err && err.name === "AbortError") return;
                // что-то пошло не так — обычный переход, страница всё равно откроется
                location.href = url;
            })
            .then(function () {
                if (pending !== controller) return;
                pending = null;
                main.classList.remove("is-loading");
                main.removeAttribute("aria-busy");
            });
    }

    function send(form, submitter) {
        var card = form.closest(".mgr-app");
        var buttons = form.querySelectorAll("button");
        // FormData с submitter — чтобы уехало значение нажатой кнопки (processed=true/false)
        var body = new URLSearchParams(new FormData(form, submitter));
        var wasProcessed = card.classList.contains("is-processed");

        buttons.forEach(function (b) { b.disabled = true; });
        note(form, "", "");

        fetch(form.action, {
            method: "POST",
            body: body,
            credentials: "same-origin",
            headers: { Accept: "application/json" },
        })
            .then(function (response) {
                // сессия истекла или пароль сменили — на вход, иначе молча ничего не сохранится
                if (response.redirected || response.status === 401) {
                    location.href = "/manager/login";
                    return null;
                }
                if (!response.ok) throw new Error(String(response.status));
                return response.json();
            })
            .then(function (data) {
                if (!data) return;
                card.classList.toggle("is-processed", data.processed);

                var status = form.querySelector(".mgr-status");
                if (data.processed && !wasProcessed) status.textContent = "Обработана " + moscowNow();
                else if (!data.processed) status.textContent = "Новая";

                if (data.processed !== wasProcessed) recount(data.processed ? 1 : -1);
                note(form, data.processed === wasProcessed ? "Сохранено" : "", "is-ok");
            })
            .catch(function () {
                note(form, "Не сохранилось — проверьте интернет и попробуйте ещё раз", "is-error");
            })
            .then(function () {
                buttons.forEach(function (b) { b.disabled = false; });
            });
    }

    function note(form, text, kind) {
        var el = form.querySelector(".mgr-note");
        if (!text) {
            if (el) el.remove();
            return;
        }
        if (!el) {
            el = document.createElement("p");
            el.setAttribute("role", "status");
            form.querySelector(".mgr-actions").appendChild(el);
        }
        el.className = "mgr-note " + kind;
        el.textContent = text;
    }

    // «новые» ↔ «обработанные»: вкладка «все» от отметки не меняется
    function recount(toDone) {
        bump("new", -toDone);
        bump("done", toDone);
    }

    function bump(name, delta) {
        var el = document.querySelector('.mgr-tabs [data-count="' + name + '"]');
        if (el) el.textContent = String(Math.max(0, (parseInt(el.textContent, 10) || 0) + delta));
    }

    // тот же формат, что рисует сервер: «dd.MM HH:mm» по Москве
    function moscowNow() {
        var parts = {};
        new Intl.DateTimeFormat("ru-RU", {
            timeZone: "Europe/Moscow", day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit", hour12: false,
        }).formatToParts(new Date()).forEach(function (p) { parts[p.type] = p.value; });
        return parts.day + "." + parts.month + " " + parts.hour + ":" + parts.minute;
    }
})();
