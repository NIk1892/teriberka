using FluentValidation;
namespace Contracts;

public abstract class SingleQueryValidator<TQuery> : FluentValueValidator<TQuery>
    where TQuery : ISingleQuery
{
    protected SingleQueryValidator()
    {
        Validate();
    }

    protected virtual void Validate()
    {
        const string validationMessage = "Get parameter is required";
        
        When(x => x.Id == Guid.Empty, () =>
        {
            RuleFor(x => x.Url).NotEmpty().WithMessage(validationMessage);
        });
        
        When(x => string.IsNullOrEmpty(x.Url), () =>
        {
            RuleFor(x => x.Id).Must(x=>x.HasValue && x != Guid.Empty).WithMessage(validationMessage);
        });
    }
}