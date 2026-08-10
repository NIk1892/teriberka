
using System.Net;

public class ExcecuteCommandException : Exception
{
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.InternalServerError;
    
    public ExcecuteCommandException()
    {

    }
    public ExcecuteCommandException(string message) : base(message)
    {

    }

    public ExcecuteCommandException(HttpStatusCode code, string message): base(message)
    {
        StatusCode = code;
    }
}