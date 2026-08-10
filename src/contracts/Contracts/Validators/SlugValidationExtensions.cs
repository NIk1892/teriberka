using Domain;
using FluentValidation;

namespace Contracts;

public static class SlugValidationExtensions
{
    public static IRuleBuilderOptions<T, string?> IsValidSlug<T>(this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .MaximumLength(Constatnts.FieldLength.Text64)
            .NotEmpty()
            .Matches("^[a-z-0-9_-]+$");
    }
}
