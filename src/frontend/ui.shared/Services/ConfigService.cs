namespace UI.Shared;

public class ConfigService(string apiUrl)
{
    public string ApiUrl { get; init; } = apiUrl;
}
