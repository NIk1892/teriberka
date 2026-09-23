using Telegram.Bot;
using Telegram.Bot.Types;

namespace Chat.Bot.Dm;

/// <summary>
/// Меню команд и описание бота в клиенте Telegram — по языкам сайта. Без языка задаётся
/// английский набор: он же наш фолбэк для всех остальных языков клиента. Сбой здесь бота
/// не выключает — команды работают и без подсказок в меню.
/// </summary>
public static class BotCommandsSetup
{
    /// <summary>Порядок = порядок в меню клиента; ключ описания — BotCmd + имя с заглавной.</summary>
    private static readonly string[] Commands =
        ["start", "book", "program", "price", "routes", "faq", "contacts", "write", "menu", "cancel"];

    public static async Task ApplyAsync(ITelegramBotClient client, ILogger logger, CancellationToken cancellationToken)
    {
        foreach (var lang in BotStrings.Languages)
        {
            var languageCode = lang == "en" ? null : lang;

            try
            {
                var commands = Commands
                    .Select(name => new BotCommand
                    {
                        Command = name,
                        Description = BotStrings.Get($"BotCmd{char.ToUpperInvariant(name[0])}{name[1..]}", lang),
                    })
                    .ToArray();

                await client.SetMyCommands(commands, new BotCommandScopeAllPrivateChats(), languageCode, cancellationToken);
                await client.SetMyDescription(BotStrings.Get("BotDescription", lang), languageCode, cancellationToken);
                await client.SetMyShortDescription(BotStrings.Get("BotShortDescription", lang), languageCode, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Не удалось задать команды и описание бота для языка {Lang}", lang);
            }
        }
    }
}
