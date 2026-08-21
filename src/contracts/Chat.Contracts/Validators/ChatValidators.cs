using Contracts;
using FluentValidation;

namespace Chat.Contracts.Validators;

public class ChatSendCommandValidator : CreateCommandValidator<ChatSendCommand>
{
    // У сообщения чата нет названия — базовое требование Title здесь неуместно.
    protected override bool TitleRequired => false;

    protected override void Validate()
    {
        base.Validate();

        RuleFor(x => x.Text)
            .NotEmpty()
            .WithMessage("Напишите вопрос")
            .MaximumLength(ChatLimits.MaxTextLength)
            .WithMessage($"Сообщение — не длиннее {ChatLimits.MaxTextLength} символов");

        // Токен приходит из cookie, которую ставили мы сами: длиннее нормы — значит подделка.
        RuleFor(x => x.SessionToken).MaximumLength(64);
        RuleFor(x => x.Culture).MaximumLength(8);
        RuleFor(x => x.Page).MaximumLength(255);
    }
}

public class ChatAdminReplyCommandValidator : CreateCommandValidator<ChatAdminReplyCommand>
{
    protected override bool TitleRequired => false;

    protected override void Validate()
    {
        base.Validate();

        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.TgMessageId).NotEqual(0);

        // Telegram и так не даёт отправить пустое сообщение, но команда приходит из внешнего
        // источника — проверяем на общих основаниях.
        RuleFor(x => x.Text)
            .NotEmpty()
            .MaximumLength(ChatLimits.MaxTextLength);
    }
}
