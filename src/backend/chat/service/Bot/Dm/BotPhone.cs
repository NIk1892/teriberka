using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Chat.Bot.Dm;

/// <summary>
/// Телефон из лички. Та же regex, что в валидаторе контракта, плюс правило длины из
/// form-ui.js сайта: у номера на «+7» ровно 11 цифр, у остальных 8..15 (E.164) — иначе
/// «+7 900» прошло бы серверную проверку обрывком.
/// </summary>
public static partial class BotPhone
{
    public const int MaxLength = 32;

    [GeneratedRegex(@"^\+?[0-9][0-9\s\-()]{6,}$")]
    private static partial Regex Pattern();

    public static bool TryNormalize(string? text, [NotNullWhen(true)] out string? phone)
    {
        phone = null;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        var value = text.Trim();
        var digits = Digits(value);

        // «8 912 345-67-89» и «7912…» без «+» — российский номер по привычке: приводим
        // к «+7», как phone-intl.js на сайте убирает приставку 8.
        if (!value.StartsWith('+') && digits.Length == 11 && digits[0] is '8' or '7')
            value = "+7" + digits[1..];

        if (value.Length > MaxLength || !Pattern().IsMatch(value))
            return false;

        digits = Digits(value);

        var lengthOk = value.StartsWith("+7", StringComparison.Ordinal)
            ? digits.Length == 11
            : digits.Length is >= 8 and <= 15;

        if (!lengthOk)
            return false;

        phone = value;
        return true;
    }

    /// <summary>Контакт из Telegram приходит без «+» («79123456789») — номер проверен Telegram'ом.</summary>
    public static string FromContact(string phoneNumber)
        => phoneNumber.StartsWith('+') ? phoneNumber : "+" + phoneNumber;

    private static string Digits(string value) => new(value.Where(char.IsAsciiDigit).ToArray());
}
