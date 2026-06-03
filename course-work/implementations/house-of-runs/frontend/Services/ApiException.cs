using System.Net;

namespace HouseOfRuns.Frontend.Services;

public sealed class ApiException(string message, HttpStatusCode statusCode) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
