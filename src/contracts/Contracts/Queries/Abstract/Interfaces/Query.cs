namespace Contracts;

public interface IRequestQuery
{
    public int? Limit { get; init; }
    public int? Offset { get; init; }
    public bool? SimpleQuery { get; init; }
    public string? Text { get; set; }
    public string? Sorting { get; set; }
}

public interface IPagedListQuery
{
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
}

public interface ISingleQuery
{
    public Guid? Id { get; init; }
    public string? Url { get; init; }
}
