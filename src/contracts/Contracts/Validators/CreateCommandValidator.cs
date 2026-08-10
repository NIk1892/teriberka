using Domain;
using FluentValidation;
namespace Contracts;

public abstract class CreateCommandValidator<TCommand> : FluentValueValidator<TCommand>
    where TCommand : Command
{
    protected virtual bool TitleRequired => true;
    
    protected CreateCommandValidator()
    {
        Validate();
    }

    protected virtual void Validate()
    {
        if (TitleRequired)
            RuleFor(x => x.Title).NotEmpty().MaximumLength(Constatnts.FieldLength.Text255);
        else
            RuleFor(x => x.Title).MaximumLength(Constatnts.FieldLength.Text255);

        if (typeof(ISlugable).IsAssignableFrom(typeof(TCommand)))
            RuleFor(x => ((ISlugable)x).Slug).IsValidSlug();

        if (typeof(IPictureable).IsAssignableFrom(typeof(TCommand)))
            RuleFor(x => ((IPictureable)x).ImagePath).MaximumLength(Constatnts.FieldLength.Text64)
                .Must(x =>x is null || !x.StartsWith("http"));
    }
}