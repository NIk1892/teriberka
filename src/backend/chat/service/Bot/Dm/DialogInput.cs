namespace Chat.Bot.Dm;

/// <summary>Что пришло от пользователя (или вернулось от эффекта) — вход машины состояний.</summary>
public abstract record DialogInput;

/// <summary>Команда «/name payload»; имя без «/» и без «@бот», в нижнем регистре.</summary>
public sealed record CommandInput(string Name, string? Payload) : DialogInput;

public sealed record TextInput(string Text) : DialogInput;

/// <summary>Контакт с reply-кнопки; IsOwn — это номер самого пользователя, а не чужой из записной книжки.</summary>
public sealed record ContactInput(string PhoneNumber, bool IsOwn) : DialogInput;

public sealed record CallbackInput(string Data) : DialogInput;

/// <summary>Стикер, фото, голос, пересланное — всё, что не текст.</summary>
public sealed record UnsupportedInput : DialogInput;

/// <summary>Результат эффекта SubmitEffect; InPlace — исходное нажатие было кнопкой.</summary>
public sealed record SubmitResultInput(SubmitResult Result, bool InPlace) : DialogInput;

public enum ForwardOutcome
{
    Sent,
    TooMany,
    Failed
}

/// <summary>Результат эффекта ForwardEffect; Announce пришёл из самого эффекта.</summary>
public sealed record ForwardResultInput(ForwardOutcome Outcome, bool Announce, bool InPlace) : DialogInput;

/// <summary>Всё, что машине нужно знать про пользователя и окружение, кроме самого диалога.</summary>
public sealed record MachineContext(
    string Lang,
    long UserId,
    string? FirstName,
    string? LastName,
    string? Username,
    BotOptions Options,
    bool HasChatSession,
    DateTime UtcNow)
{
    /// <summary>Имя из профиля Telegram для кнопки «использовать имя»; пусто — кнопки нет.</summary>
    public string TelegramName => string.Join(' ', new[] { FirstName, LastName }
        .Where(part => !string.IsNullOrWhiteSpace(part))
        .Select(part => part!.Trim()));
}
