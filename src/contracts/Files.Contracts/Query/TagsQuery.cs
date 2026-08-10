using Contracts;
using News.Contracts;

namespace News.Contracts;

public class TagSingleQuery : Query<TagDto>
{
    public TagSingleQuery()
    {
        
    }
    
    public TagSingleQuery(Guid id):base(id)
    {
        
    }
}
public class TagListQuery : ListQuery<TagDto>;

public class TagQueryPagedList : PagedListQuery<TagDto, TagListQuery>;

