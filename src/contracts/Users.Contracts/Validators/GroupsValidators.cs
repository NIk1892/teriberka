
using Contracts;
using FluentValidation;
using Users.Domain;
using BaseDomain = Domain;

namespace Users.Contracts;

public class GroupSingleQueryValidator : SingleQueryValidator<GroupSingleQuery>;

public class GroupCreateCommandValidator : CreateCommandValidator<GroupCreateCommand>
{
    protected override bool TitleRequired => false;

    protected override void Validate()
    {
        base.Validate();
        Validate(this);
    }

    public static void Validate<T>(AbstractValidator<T> validator) where T : GroupCreateCommand
    {
        var groupTypes = Enum.GetValues<GroupType>();
        validator.RuleFor(x => x.Type).Must(x => groupTypes.Contains(x));
    }
}

public class GroupUpdateCommandValidator : UpdateCommandValidator<GroupUpdateCommand>
{
    protected override bool TitleRequired => false;
    protected override void Validate()
    {
        base.Validate();
        GroupCreateCommandValidator.Validate(this);
    }
}

