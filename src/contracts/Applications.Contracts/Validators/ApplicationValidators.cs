using Contracts;
using FluentValidation;

namespace Applications.Contracts.Validators;

public class ApplicationCreateCommandValidator : CreateCommandValidator<ApplicationCreateCommand>
{
    // Имя необязательно: единственное, что нужно оператору для звонка, — телефон.
    protected override bool TitleRequired => false;

    protected override void Validate()
    {
        RuleFor(x => x.Title)
            .MaximumLength(255).WithMessage("Имя — не длиннее 255 символов");

        // Телефон уходит оператору и в бота, поэтому в поле должен лежать телефон,
        // а не произвольный текст со ссылками — иначе форма превращается в канал спама.
        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Укажите телефон в формате +7 900 000-00-00")
            .MaximumLength(32).WithMessage("Укажите телефон в формате +7 900 000-00-00")
            .Matches(@"^\+?[0-9][0-9\s\-()]{6,}$")
            .WithMessage("Укажите телефон в формате +7 900 000-00-00");

        // В поле приходит код направления из выпадающего списка — принимаем только
        // известные, чтобы подделанный POST не записал в заявку произвольную строку.
        RuleFor(x => x.Route)
            .Must(ApplicationRoutes.IsKnown)
            .WithMessage("Выберите маршрут из списка");
    }
}
