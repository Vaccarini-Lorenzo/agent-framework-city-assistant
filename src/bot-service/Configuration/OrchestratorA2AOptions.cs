namespace BotService.Configuration;

internal sealed class OrchestratorA2AOptions
{
    public const string SectionName = "A2A:Orchestrator";

    public string BaseUrl { get; set; } = "http://localhost:5197";
    public string AgentCardPath { get; set; } = "/.well-known/agent-card.json";
    public string FallbackMessageEndpointPath { get; set; } = "/agenta2a";
}