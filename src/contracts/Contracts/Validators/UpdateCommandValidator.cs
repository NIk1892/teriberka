namespace Contracts;

public abstract class UpdateCommandValidator<TCommand> : CreateCommandValidator<TCommand>
    where TCommand : Command
{
    protected UpdateCommandValidator()
    {
        Validate();
    }

    protected virtual void Validate()
    {
        base.Validate();
    }
}