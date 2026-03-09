using A2A;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.A2A;

public static class A2AExtension
{
    public static IServiceCollection AddA2AAgents(this IServiceCollection services)
    {
        services.AddSingleton<A2AAgent>(serviceProvider =>
        {
            // var orchestratorAgentUrl = Environment.GetEnvironmentVariable("services__orchestrator__https__0")
            //     ?? Environment.GetEnvironmentVariable("services__orchestrator__http__0");
            string orchestratorAgentUrl = "https://localhost:7197";

            IHttpClientFactory httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            HttpClient orchestratorHttpClient = httpClientFactory.CreateClient(nameof(A2AExtension));
            orchestratorHttpClient.BaseAddress = new Uri(orchestratorAgentUrl);
            orchestratorHttpClient.Timeout = TimeSpan.FromSeconds(60);

            var resolver = new A2ACardResolver(
                orchestratorHttpClient.BaseAddress!,
                orchestratorHttpClient,
                agentCardPath: "/agenta2a/v1/card");
            
            return (A2AAgent)resolver.GetAIAgentAsync().Result;
        });


        return services;
    }
}