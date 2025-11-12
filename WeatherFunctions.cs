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

    [Function(nameof(GetWeatherHttp))]
    public async Task<IActionResult> GetWeatherHttp(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "weather")] HttpRequest req)
    {
        _logger.LogInformation("Processing weather HTTP request.");

        if (!double.TryParse(req.Query["latitude"], out var latitude))
        {
            return new BadRequestObjectResult("Invalid or missing 'latitude' query parameter.");
        }

        if (!double.TryParse(req.Query["longitude"], out var longitude))
        {
            return new BadRequestObjectResult("Invalid or missing 'longitude' query parameter.");
        }

        var forecast = await FetchWeatherForecastAsync(latitude, longitude);
        return new OkObjectResult(forecast);
    }

    [Function(nameof(GetWeatherMcp))]
    public async Task<string> GetWeatherMcp(
        [McpToolTrigger("get_weather", "Get current conditions and weather forecast for a location.")]
        ToolInvocationContext context,
        [McpToolProperty("latitude", "Latitude of the location.", isRequired: true)]
        double latitude,
        [McpToolProperty("longitude", "Longitude of the location.", isRequired: true)]
        double longitude)
    {
        return await FetchWeatherForecastAsync(latitude, longitude);
    }

    private async Task<string> FetchWeatherForecastAsync(double latitude, double longitude)
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

    [Function(nameof(GetAuthComplete))]
    public async Task<IActionResult> GetAuthComplete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "authcomplete")] HttpRequest req)
    {
        _logger.LogInformation("Processing authcomplete HTTP request.");

        var htmlContent = await File.ReadAllTextAsync("authcomplete.html");
        return new ContentResult
        {
            Content = htmlContent,
            ContentType = "text/html",
            StatusCode = 200
        };
    }
}