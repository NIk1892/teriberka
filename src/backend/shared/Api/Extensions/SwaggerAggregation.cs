using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Api;

public static class GatewaySwaggerExtensions
{
    public static void MapGatewaySwagger(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return;

        var endpoints = app.Configuration.GetSection("SwaggerEndpoints")
            .GetChildren().ToDictionary(x => x.Key, x => x.Value!);

        if (endpoints.Count == 0) return;

        var pathFilters = app.Configuration.GetSection("SwaggerPathFilters").Get<string[]>();

        foreach (var (key, url) in endpoints)
        {
            app.MapGet($"/swagger/{key}/swagger.json", async (IHttpClientFactory factory) =>
            {
                var http = factory.CreateClient("swagger");
                http.Timeout = TimeSpan.FromSeconds(5);
                var json = await http.GetStringAsync(url);

                if (pathFilters is { Length: > 0 })
                    json = FilterSwaggerPaths(json, pathFilters);

                return Results.Content(json, "application/json");
            }).ExcludeFromDescription();
        }

        app.UseSwaggerUI(c =>
        {
            foreach (var key in endpoints.Keys)
                c.SwaggerEndpoint($"/swagger/{key}/swagger.json", key);
        });
    }

    private static string FilterSwaggerPaths(string json, string[] allowedPrefixes)
    {
        var doc = JsonNode.Parse(json);
        if (doc?["paths"] is not JsonObject paths) return json;

        var keysToRemove = paths
            .Select(p => p.Key)
            .Where(k => !allowedPrefixes.Any(prefix =>
                k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var key in keysToRemove)
            paths.Remove(key);

        return doc.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }
}
