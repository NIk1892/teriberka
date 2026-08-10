using System.Text;
using Contracts;
using Mediator;

namespace UI.Shared.Helpers;

public static class QueryBuilder
{
    public static string ToQueryString(this IBaseRequest request, string baseUrl, bool append = false)
    {
        var queryBuilder = new StringBuilder(baseUrl);

        if (!append)
            queryBuilder.Append('?');

        if (request is ISingleQuery singleRequest)
            queryBuilder.Append(singleRequest.Id.HasValue ? $"id={singleRequest.Id}" : $"url={singleRequest.Url}");

        if (request is IRequestQuery listRequest)
        {
            if (listRequest.Limit > 0)
            {
                queryBuilder.Append($"limit={listRequest.Limit}");

                if (listRequest.Offset.HasValue)
                    queryBuilder.Append($"&offset={listRequest.Offset}");
            }

            if (!string.IsNullOrWhiteSpace(listRequest.Text))
                queryBuilder.Append($"&text={listRequest.Text}");

            if (!string.IsNullOrWhiteSpace(listRequest.Sorting))
                queryBuilder.Append($"&sorting={listRequest.Sorting}");

            if (listRequest.SimpleQuery == true)
                queryBuilder.Append("&simpleQuery=true");
        }

        if (request is IPagedListQuery pagedListRequest)
            queryBuilder.Append($"pageIndex={pagedListRequest.PageIndex}&pageSize={pagedListRequest.PageSize}");

        return queryBuilder.ToString();
    }
}
