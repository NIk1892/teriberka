namespace UI.Shared;

public class FileService(string baseUrl)
{
    private readonly string _baseUrl = baseUrl.TrimEnd('/');

    public string GetUrl(string url) => $"{_baseUrl}/{url.TrimStart('/')}";
}
