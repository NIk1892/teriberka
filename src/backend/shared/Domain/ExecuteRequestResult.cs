using System.Net;

namespace Domain;

public record ExecuteRequestResult(
    HttpStatusCode StatusCode = HttpStatusCode.OK,
    Guid? Id = null,
    string? Value = null,
    uint? Hash = null)
{
    public bool IsSuccess => StatusCode is HttpStatusCode.OK or HttpStatusCode.Created;
    public static ExecuteRequestResult Success => new();
}