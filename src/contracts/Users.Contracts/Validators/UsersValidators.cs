
using Contracts;
using FluentValidation;
using BaseDomain = Domain;

namespace Users.Contracts;

public class UserSingleQueryValidator : SingleQueryValidator<UserSingleQuery>
{
    protected override void Validate()
    {
        When(x => string.IsNullOrEmpty(x.TgId) && string.IsNullOrEmpty(x.Email), () =>
        {
            base.Validate();
        });
    }
}

public class UserCreateCommandValidator : CreateCommandValidator<UserCreateCommand>
{
    protected override bool TitleRequired => false;

    protected override void Validate()
    {
        base.Validate();
        Validate(this);
    }

    public static void Validate<T>(AbstractValidator<T> validator) where T : UserCreateCommand
    {
        validator.RuleFor(x => x.FirstName).MaximumLength(BaseDomain.Constatnts.FieldLength.Text255);
        validator.RuleFor(x => x.LastName).MaximumLength(BaseDomain.Constatnts.FieldLength.Text255);
        validator.When(x => string.IsNullOrEmpty(x.TgId), () =>
        {
            validator.RuleFor(x => x.Email).NotEmpty().EmailAddress();
        });
        validator.When(x => !string.IsNullOrEmpty(x.Email), () =>
        {
            validator.RuleFor(x => x.Email).EmailAddress();
        });
    }
}

public class UserUpdateCommandValidator : UpdateCommandValidator<UserUpdateCommand>
{
    protected override bool TitleRequired => false;
    protected override void Validate()
    {
        base.Validate();
        UserCreateCommandValidator.Validate(this);
    }
}

// public class UsersCommandValidator : CreateCommandValidator<UserCreateCommand>
// {
//     protected override void Validate()
//     {
//         When(q => string.IsNullOrEmpty(q.TgId),
//             () =>
//             {
//                 RuleFor(x => x.Email).EmailAddress().NotEmpty().MaximumLength(BaseDomain.Constatnts.FieldLength.Text64);
//             });
//
//         When(q => string.IsNullOrEmpty(q.Email),
//             () => { RuleFor(x => x.TgId).NotEmpty().MaximumLength(BaseDomain.Constatnts.FieldLength.Text32); });
//         
//         RuleFor(x => x.FirstName).MaximumLength(BaseDomain.Constatnts.FieldLength.Text128);
//         RuleFor(x => x.LastName).MaximumLength(BaseDomain.Constatnts.FieldLength.Text128);
//     }
// }