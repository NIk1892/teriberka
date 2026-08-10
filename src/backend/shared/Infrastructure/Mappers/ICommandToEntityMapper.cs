using Contracts;
using Domain;

namespace Infrastructure.Mappers;

public interface ICommandToEntityMapper<TEntity, in TSource>
    where TEntity : IEntity
    where TSource : ICommand
{
    TEntity ToNewEntity(TSource source);
    void ToEntity(TSource source, TEntity target) => throw new NotSupportedException($"Update mapping not implemented for {GetType().Name}");
}