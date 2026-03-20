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
            var orchestratorAgentUrlString = Environment.GetEnvironmentVariable("services__orchestratoragent__https__0")
                ?? Environment.GetEnvironmentVariable("services__orchestratoragent__http__0");
            
            if (string.IsNullOrEmpty(orchestratorAgentUrlString))
            {
                throw new InvalidOperationException("Orchestrator Agent URL is not configured. Please set the environment variable 'services__orchestrator__https__0' or 'services__orchestrator__http__0'.");
            }

            IHttpClientFactory httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            HttpClient orchestratorHttpClient = httpClientFactory.CreateClient(nameof(A2AExtension));
            orchestratorHttpClient.BaseAddress = new Uri(orchestratorAgentUrlString);
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