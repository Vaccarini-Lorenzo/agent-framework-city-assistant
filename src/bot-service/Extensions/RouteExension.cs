using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Builder;

public static class RouteExtension
{
    public static WebApplication ConfigureRoutes(this WebApplication app)
    {
        // FOR DEMO PURPOSES ONLY!
        // In any other scenario you should use .RequireAuthorization() to trigger the JWT validation middleware
        // Without enforcing authorization, any message with empty "Authorization" header will be accepted and processed by the bot.
        app.MapPost("/api/messages",
                    async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
                    {
                        await adapter.ProcessAsync(request, response, agent, cancellationToken);                        
                    });
                    //.RequireAuthorization();
        return app;
    }
}