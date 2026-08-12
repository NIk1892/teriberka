using Contracts;
using FluentValidation;

namespace Applications.Contracts.Validators;

public class ApplicationCreateCommandValidator : CreateCommandValidator<ApplicationCreateCommand>
{
    protected override void Validate()
    {
        base.Validate();

        // Телефон уходит оператору и в бота, поэтому в поле должен лежать телефон,
        // а не произвольный текст со ссылками — иначе форма превращается в канал спама.
        RuleFor(x => x.Phone)
            .NotEmpty()
            .MaximumLength(32)
            .Matches(@"^\+?[0-9][0-9\s\-()]{6,}$")
            .WithMessage("Укажите телефон в формате +7 900 000-00-00");

        RuleFor(x => x.PeopleCount).GreaterThan(0).LessThanOrEqualTo(50);

        RuleFor(x => x.ArrivalDate)
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow.Date))
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow.Date.AddYears(2)))
            .WithMessage("Дата приезда должна быть в пределах двух лет");
    }
}
