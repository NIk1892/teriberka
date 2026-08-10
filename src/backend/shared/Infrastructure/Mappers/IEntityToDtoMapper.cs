using Domain;

namespace Infrastructure.Mappers;

public interface IEntityToDtoMapper<out TDto, in TSource>
    where TDto: IDto
    where TSource: IEntity
{
    IQueryable<TDto> ToDto(IQueryable<TSource> source);
}