namespace APIBenchmark;

public class RequestParams
{
    public string Name { get; set; }
    public Method Type { get; set; }
    public string Endpoint { get; set; }
    public string BearerToken { get; set; }
    public string Body { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
}

public class LoadParams
{
   
}

public enum Method
{
    POST,
    GET,
    PUT,
    DELETE
}

public static class MethodExtensions
{
    public static HttpMethod ToHttpMethod(this Method method)
    {
        return method switch
        {
            Method.POST => HttpMethod.Post,
            Method.GET => HttpMethod.Get,
            Method.PUT => HttpMethod.Put,
            Method.DELETE => HttpMethod.Delete,
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
        };
    }
}
