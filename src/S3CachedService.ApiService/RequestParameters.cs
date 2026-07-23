namespace S3CachedService.ApiService;

public class RequestParameters : Dictionary<string, string[]>
{
    private RequestParameters(IEnumerable<KeyValuePair<string, string[]>> values)
        : base(values, StringComparer.OrdinalIgnoreCase)
    {
    }

    public static RequestParameters From(HttpRequest httpRequest)
    {
        var values = httpRequest.Query
            .GroupBy(x => x.Key, x => x.Value)
            .Select(x => KeyValuePair.Create(x.Key, x.Select(s => s.ToString()).ToArray()))
            .ToList();

        return new(values);
    }

    public string? FileName => TryGetValue("fileName", out var fileName) ? fileName.FirstOrDefault() : null;
}
