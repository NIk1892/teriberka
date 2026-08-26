// Клиентская валидация формы заявки и маска телефона. Стандартный HTML5 required
// подсвечивает только первое невалидное поле системным пузырём — здесь проверяем
// все поля сразу и показываем подсказки под каждым в стилистике сайта. Телефон
// форматируется на лету: в поле остаются только цифры, разметку «+7 900 000-00-00»
// скрипт расставляет сам. Без JavaScript остаются HTML5-атрибуты
// (required/maxlength/inputmode) и серверная валидация — маска только помогает.
(function () {
    "use strict";

    // то же правило, что в ApplicationCreateCommandValidator
    var PHONE_RE = /^\+?[0-9][0-9\s\-()]{6,}$/;

    document.querySelectorAll(".apply-form").forEach(initForm);

    function initForm(form) {
        form.setAttribute("novalidate", "");

        var phoneInput = form.querySelector('[data-field="Phone"] input');
        if (phoneInput)
            initPhoneMask(phoneInput);

        var msgs = {
            phone: form.dataset.valPhone || "Invalid phone",
            consent: form.dataset.valConsent || "Required",
        };

        form.addEventListener("submit", function (e) {
            clearErrors(form);
            var errors = collectErrors(form, msgs);
            if (errors.length === 0)
                return;

            e.preventDefault();
            errors.forEach(function (item) {
                showError(form, item.field, item.message);
            });

            var first = form.querySelector(".field-invalid input");
            if (first)
                first.focus({ preventScroll: true });
        });

        // ошибка снимается, как только пользователь начал исправлять поле
        ["input", "change"].forEach(function (type) {
            form.addEventListener(type, function (e) {
                var field = e.target && e.target.closest("[data-field]");
                if (field)
                    clearFieldError(field);
            });
        });
    }

    function collectErrors(form, msgs) {
        var errors = [];

        var phone = form.querySelector('[data-field="Phone"] input');
        var phoneVal = phone ? phone.value.trim() : "";
        // Сервер принимает любой телефон по PHONE_RE; браузер строже — он знает,
        // сколько цифр в номере: 11 у российского, не меньше 8 у зарубежного.
        // Иначе «+7 900» проходило бы проверку и уезжало оператору обрывком.
        var digits = onlyDigits(phoneVal).length;
        var enough = phoneVal.indexOf("+7") === 0 ? digits === 11 : digits >= 8;
        if (!phoneVal || phoneVal.length > 32 || !PHONE_RE.test(phoneVal) || !enough)
            errors.push({ field: "Phone", message: msgs.phone });

        var consent = form.querySelector('[data-field="Consent"] input[type="checkbox"]');
        if (!consent || !consent.checked)
            errors.push({ field: "Consent", message: msgs.consent });

        return errors;
    }

    // ---- маска телефона ----

    // Российский номер набирают чаще всего, поэтому «7…», «8…» и «9…» приводятся
    // к +7 и раскладываются по шаблону плейсхолдера. Зарубежный номер узнаётся по
    // явному «+» с другим кодом — там разметку не навязываем, оставляем «+» и цифры.
    function initPhoneMask(input) {
        input.addEventListener("input", function () {
            var caret = input.selectionStart === null ? input.value.length : input.selectionStart;
            var digitsBefore = onlyDigits(input.value.slice(0, caret)).length;
            var masked = maskPhone(input.value);
            if (masked.value === input.value)
                return;

            input.value = masked.value;
            // маска могла подставить код страны (набрали «900…» — стало «+7 900…»),
            // тогда цифр слева от курсора стало на одну больше: без этой поправки
            // курсор вставал сразу после «7» и следующая цифра лезла в начало номера
            var pos = caretAfterDigits(masked.value, digitsBefore + masked.added);
            try { input.setSelectionRange(pos, pos); } catch (err) { /* поле вне фокуса */ }
        });
    }

    // возвращает { value, added }: added = 1, если код страны дописан маской,
    // а не набран человеком (нужно для позиции курсора)
    function maskPhone(raw) {
        var plus = raw.trim().charAt(0) === "+";
        var digits = onlyDigits(raw);
        if (!digits)
            return { value: plus ? "+" : "", added: 0 };

        var first = digits.charAt(0);
        if (plus && first !== "7" && first !== "8")
            return { value: "+" + digits.slice(0, 15), added: 0 };

        var added = 0;
        if (first === "8")
            digits = "7" + digits.slice(1);
        else if (first !== "7") {
            digits = "7" + digits;
            added = 1;
        }

        var rest = digits.slice(1, 11);
        var out = "+7";
        if (rest.length) out += " " + rest.slice(0, 3);
        if (rest.length > 3) out += " " + rest.slice(3, 6);
        if (rest.length > 6) out += "-" + rest.slice(6, 8);
        if (rest.length > 8) out += "-" + rest.slice(8, 10);
        return { value: out, added: added };
    }

    function onlyDigits(value) {
        return value.replace(/\D/g, "");
    }

    // курсор возвращается к той же цифре, что была перед ним до форматирования —
    // иначе правка в середине номера каждый раз выбрасывала бы его в конец
    function caretAfterDigits(value, count) {
        if (count <= 0)
            return value.charAt(0) === "+" ? 1 : 0;

        var seen = 0;
        for (var i = 0; i < value.length; i++) {
            if (value.charCodeAt(i) >= 48 && value.charCodeAt(i) <= 57) {
                seen++;
                if (seen === count)
                    return i + 1;
            }
        }
        return value.length;
    }

    function clearErrors(form) {
        form.querySelectorAll("[data-field]").forEach(clearFieldError);
    }

    function clearFieldError(field) {
        field.classList.remove("field-invalid");
        var hint = field.querySelector(".field-hint-error");
        if (hint)
            hint.remove();
        field.querySelectorAll("[aria-invalid]").forEach(function (el) {
            el.removeAttribute("aria-invalid");
        });
    }

    function showError(form, fieldName, message) {
        var field = form.querySelector('[data-field="' + fieldName + '"]');
        if (!field)
            return;

        field.classList.add("field-invalid");
        field.querySelectorAll("input").forEach(function (el) {
            el.setAttribute("aria-invalid", "true");
        });

        var hint = document.createElement("span");
        hint.className = "field-hint field-hint-error";
        hint.setAttribute("role", "alert");
        hint.textContent = message;
        field.appendChild(hint);
    }
})();
