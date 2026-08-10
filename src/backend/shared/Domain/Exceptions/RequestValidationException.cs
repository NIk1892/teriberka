
using System.Net;
using Domain;

public class RequestValidationException : Exception
{
    public readonly ExecuteRequestResult? ExecuteResult;
    
    public RequestValidationException()
    {

    }
    public RequestValidationException(string message) : base(message)
    {

    }

    public RequestValidationException(ExecuteRequestResult executeResult)
    {
        ExecuteResult = executeResult;
    }

    public RequestValidationException(HttpStatusCode code)
    {
        ExecuteResult = new ExecuteRequestResult(code);
    }
}