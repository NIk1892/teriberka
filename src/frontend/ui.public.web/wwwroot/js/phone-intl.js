// Международный ввод телефона в форме заявки: слева плашка страны (флаг + код),
// справа — ТОЛЬКО национальный номер. Код в поле не дублируется (владелец,
// 26.08.2026: «+7» в плашке и «+7» в номере читались как два кода): видимое
// значение — «912 345-67-89», а полный номер «+7 912 345-67-89» скрипт собирает
// сам в момент отправки формы (capture-фаза submit — раньше валидации form-ui.js)
// и возвращает поле к национальному виду, если отправку остановила валидация.
// Сервер, контракт и валидатор по-прежнему видят одну строку с кодом.
//
// Прогрессивное улучшение: разметка селектора есть всегда, но видимой её делает
// класс js-phone на <html> — без JavaScript остаётся обычное поле, куда номер
// вводится с кодом руками. Данные о странах скрипт читает из data-* атрибутов
// пунктов: единственный источник списка — C# (Features/Contacts/PhoneCountries.cs),
// дублировать его здесь нельзя.
(function () {
    "use strict";

    var wrap = document.querySelector(".phone-input");
    if (!wrap) return;

    var input = wrap.querySelector('input[type="tel"]');
    var picker = wrap.querySelector(".phone-cc");
    var items = picker ? Array.prototype.slice.call(picker.querySelectorAll(".phone-cc-item")) : [];
    if (!input || !picker || !items.length) return;

    var countries = items.map(function (el) {
        return {
            el: el,
            iso: el.dataset.iso,
            code: el.dataset.code,
            trunk: el.dataset.trunk || "",
            len: parseInt(el.dataset.len, 10) || 10,
            // «3 3-2-2» → размеры групп и разделители между ними
            sizes: el.dataset.groups.split(/[^0-9]/).filter(Boolean).map(Number),
            seps: el.dataset.groups.replace(/[0-9]/g, "").split(""),
        };
    });

    var flagUse = picker.querySelector("summary .flag use");
    var codeLabel = picker.querySelector(".phone-cc-current");

    var current = countries[0];

    document.documentElement.classList.add("js-phone");

    // ---- разбор и форматирование ----

    function onlyDigits(value) {
        return value.replace(/\D/g, "");
    }

    // страна по международному номеру: побеждает самый длинный совпавший код
    function detect(digits) {
        var best = null;
        for (var i = 0; i < countries.length; i++) {
            var c = countries[i];
            if (digits.indexOf(c.code) === 0 && (!best || c.code.length > best.code.length)) best = c;
        }
        return best;
    }

    function groupNational(nat, country) {
        var out = "", pos = 0;
        for (var g = 0; g < country.sizes.length && pos < nat.length; g++) {
            if (g > 0) out += country.seps[g - 1] || " ";
            out += nat.slice(pos, pos + country.sizes[g]);
            pos += country.sizes[g];
        }
        if (pos < nat.length) out += " " + nat.slice(pos); // номер длиннее ожидаемого — хвост как есть
        return out;
    }

    // Разбор ввода → { value: национальный вид для поля, country: смена страны
    // или null, foreign: true = чужой код, номер остаётся в поле целиком с «+» }.
    function build(raw) {
        var plus = raw.trim().charAt(0) === "+";
        var digits = onlyDigits(raw);
        if (!digits) return { value: plus ? "+" : "", country: null, foreign: false };

        var country = null;
        if (plus) {
            // явный «+код»: вставили или набрали международный номер — код уходит
            // в плашку, в поле остаётся национальная часть
            country = detect(digits);
            if (!country) return { value: "+" + digits.slice(0, 15), country: null, foreign: true };
            digits = digits.slice(country.code.length);
        } else {
            country = current;
            if (country.trunk && digits.charAt(0) === country.trunk) {
                // «8 912…» → «912…»: внутреннюю приставку съедает маска
                digits = digits.slice(1);
            } else if (digits.indexOf(country.code) === 0 && digits.length > country.len) {
                // вставили «79123456789» без плюса — лишний код тоже убираем
                digits = digits.slice(country.code.length);
            }
        }

        var nat = digits.slice(0, country.len);
        return { value: groupNational(nat, country), country: country, foreign: false };
    }

    // курсор возвращается к той же цифре, что была перед ним до форматирования
    function caretAfterDigits(value, count) {
        if (count <= 0) return value.charAt(0) === "+" ? 1 : 0;
        var seen = 0;
        for (var i = 0; i < value.length; i++) {
            if (value.charCodeAt(i) >= 48 && value.charCodeAt(i) <= 57) {
                seen++;
                if (seen === count) return i + 1;
            }
        }
        return value.length;
    }

    // ---- вид селектора ----

    function showCountry(country) {
        current = country;
        if (flagUse) flagUse.setAttribute("href", "#flag-" + country.iso);
        if (codeLabel) codeLabel.textContent = "+" + country.code;
        // плейсхолдер — национальный образец без кода: код уже виден в плашке
        input.placeholder = groupNational(new Array(country.len + 1).join("0"), country);
        countries.forEach(function (c) {
            c.el.classList.toggle("is-current", c === country);
            c.el.setAttribute("aria-pressed", c === country ? "true" : "false");
        });
    }

    // ---- события ----

    input.addEventListener("input", function () {
        var caret = input.selectionStart === null ? input.value.length : input.selectionStart;
        var digitsBefore = onlyDigits(input.value.slice(0, caret)).length;
        var before = onlyDigits(input.value).length;
        var res = build(input.value);
        if (res.country && res.country !== current) showCountry(res.country);
        if (res.value === input.value) return;

        input.value = res.value;
        // маска могла съесть цифры слева от курсора (код страны, приставку «8») —
        // сдвигаем позицию на столько же, иначе курсор прыгал внутрь номера
        var removed = before - onlyDigits(res.value).length;
        var pos = caretAfterDigits(res.value, digitsBefore - (removed > 0 ? removed : 0));
        try { input.setSelectionRange(pos, pos); } catch (err) { /* поле вне фокуса */ }
    });

    // Отправка: собираем полный номер до того, как его увидит валидация form-ui.js.
    // Capture-фаза на document срабатывает раньше bubble-обработчика формы,
    // поэтому порядок подключения скриптов здесь не важен.
    document.addEventListener("submit", function (e) {
        if (!e.target || !e.target.contains(input)) return;
        var val = input.value.trim();
        if (!val || val.charAt(0) === "+") return; // пусто или чужой код — как есть
        var national = val;
        input.value = "+" + current.code + " " + national;
        // если валидация не пустила форму, возвращаем национальный вид;
        // при успешной отправке страница уходит и таймер уже не важен
        setTimeout(function () { input.value = national; }, 60);
    }, true);

    picker.addEventListener("click", function (e) {
        var item = e.target.closest(".phone-cc-item");
        if (!item) return;
        e.preventDefault();

        var next = countries.filter(function (c) { return c.el === item; })[0];
        if (!next) return;

        // национальную часть сохраняем: человек мог набрать номер, а потом
        // вспомнить, что страна другая
        var nat = onlyDigits(input.value);

        showCountry(next);
        picker.open = false;
        input.value = nat ? groupNational(nat.slice(0, next.len), next) : "";
        input.focus();
        try { input.setSelectionRange(input.value.length, input.value.length); } catch (err) { /* не критично */ }
    });

    // Esc закрывает список — того же поведения ждут от бургера и виджета связи
    picker.addEventListener("keydown", function (e) {
        if (e.key === "Escape" && picker.open) {
            picker.open = false;
            picker.querySelector("summary").focus();
        }
    });

    showCountry(current);
    // номер мог приехать с сервера (форма вернулась с ошибкой): «+7 912…»
    // раскладываем на плашку и национальную часть, набранный без плюса
    // разбираем как национальный — build сам съест приставку «8»
    if (input.value) {
        var initial = build(input.value);
        if (initial.country && initial.country !== current) showCountry(initial.country);
        if (!initial.foreign) input.value = initial.value;
    }
}());
