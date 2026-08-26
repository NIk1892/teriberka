// Клиентская валидация формы заявки. Стандартный HTML5 required подсвечивает
// только первое невалидное поле системным пузырём — здесь проверяем все поля сразу
// и показываем подсказки под каждым в стилистике сайта. Форматированием телефона
// занимается phone-intl.js (селектор страны и маска по её правилам), здесь остались
// только проверки. Без JavaScript работают HTML5-атрибуты (required/maxlength/
// inputmode) и серверная валидация.
(function () {
    "use strict";

    // то же правило, что в ApplicationCreateCommandValidator
    var PHONE_RE = /^\+?[0-9][0-9\s\-()]{6,}$/;

    document.querySelectorAll(".apply-form").forEach(initForm);

    function initForm(form) {
        form.setAttribute("novalidate", "");

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
        // «+7» — российский и казахстанский формат: ровно 11 цифр; у остальных
        // проверяем только диапазон E.164, длины у стран разные
        var enough = phoneVal.indexOf("+7") === 0 ? digits === 11 : (digits >= 8 && digits <= 15);
        if (!phoneVal || phoneVal.length > 32 || !PHONE_RE.test(phoneVal) || !enough)
            errors.push({ field: "Phone", message: msgs.phone });

        var consent = form.querySelector('[data-field="Consent"] input[type="checkbox"]');
        if (!consent || !consent.checked)
            errors.push({ field: "Consent", message: msgs.consent });

        return errors;
    }

    function onlyDigits(value) {
        return value.replace(/\D/g, "");
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
