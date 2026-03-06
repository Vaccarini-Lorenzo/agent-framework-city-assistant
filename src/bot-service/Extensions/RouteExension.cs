using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Builder;

public static class RouteExtension
{
    public static WebApplication ConfigureRoutes(this WebApplication app)
    {
        // Default
        app.MapGet("/", () => "Microsoft Agents SDK + MAF Sample");
        // For development purposes only.
        app.Urls.Add("http://localhost:3978");
        // Route configured for Bot Service
        app.MapPost("/api/messages",async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) => await adapter.ProcessAsync(request, response, agent, cancellationToken));
        return app;
    }
}