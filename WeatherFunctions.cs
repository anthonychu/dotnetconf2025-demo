using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Company.Function;

public class WeatherFunctions
{
    private readonly ILogger<WeatherFunctions> _logger;
    private readonly HttpClient _httpClient;

    public WeatherFunctions(ILogger<WeatherFunctions> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    [Function("Negotiate")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "signalr/{*path}")] HttpRequest req,
        [SignalRConnectionInfoInput(HubName = "serverless")] string connectionInfo,
        string? path)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        
        if (path?.Equals("negotiate", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new OkObjectResult(connectionInfo);
        }
        
        return new NotFoundResult();
    }

    [Function(nameof(GetWeather))]
    public async Task<string> GetWeather(
        [McpToolTrigger("get_weather", "Get current conditions and weather forecast for a location.")]
        ToolInvocationContext context,
        [McpToolProperty("latitude", "Latitude of the location.", isRequired: true)]
        double latitude,
        [McpToolProperty("longitude", "Longitude of the location.", isRequired: true)]
        double longitude)
    {
        try
        {
            var pointUrl = string.Create(CultureInfo.InvariantCulture, $"/points/{latitude},{longitude}");
            using var jsonDocument = await _httpClient.ReadJsonDocumentAsync(pointUrl);
            var forecastUrl = jsonDocument.RootElement.GetProperty("properties").GetProperty("forecast").GetString()
                ?? throw new Exception($"No forecast URL provided by {_httpClient.BaseAddress}points/{latitude},{longitude}");

            using var forecastDocument = await _httpClient.ReadJsonDocumentAsync(forecastUrl);
            var periods = forecastDocument.RootElement.GetProperty("properties").GetProperty("periods").EnumerateArray();

            var forecast = string.Join("\n---\n", periods.Select(period => $"""
                    {period.GetProperty("name").GetString()}
                    Temperature: {period.GetProperty("temperature").GetInt32()}°F
                    Wind: {period.GetProperty("windSpeed").GetString()} {period.GetProperty("windDirection").GetString()}
                    Forecast: {period.GetProperty("detailedForecast").GetString()}
                    """));

            return forecast;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            var errorMessage = $"Unable to get weather forecast for location (latitude: {latitude}, longitude: {longitude}). This location may not be supported by the weather service.";

            return errorMessage;
        }
    }
}