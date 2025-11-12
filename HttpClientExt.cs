using System.Text.Json;

internal static class HttpClientExt
{
    public static async Task<JsonDocument> ReadJsonDocumentAsync(this HttpClient client, string requestUri)
    {
        // If requestUri is absolute, create a new request; otherwise use the client's BaseAddress
        var uri = Uri.IsWellFormedUriString(requestUri, UriKind.Absolute) 
            ? new Uri(requestUri) 
            : new Uri(client.BaseAddress!, requestUri);
        
        Console.WriteLine($"Calling URL: {uri}");
        
        using var response = await client.GetAsync(uri);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }
}