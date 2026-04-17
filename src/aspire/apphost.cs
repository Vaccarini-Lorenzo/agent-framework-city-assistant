#:sdk Aspire.AppHost.Sdk@13.1.1
#:package Aspire.Hosting.AppHost@13.0.0
#:package Aspire.Hosting.Azure.AIFoundry@13.1.1-preview.1.26105.8
#:package Aspire.Hosting.Azure.CosmosDB@13.1.1
#:package Aspire.Hosting.JavaScript@13.1.1
#:package Aspire.Hosting.Yarp@13.1.1

#:project ../restaurant-agent/RestaurantAgent.csproj
#:project ../activities-agent/ActivitiesAgent.csproj
#:project ../accommodation-agent/AccommodationAgent.csproj
#:project ../orchestrator-agent/OrchestratorAgent.csproj
#:project ../geocoding-mcp-server/GeocodingMcpServer.csproj
#:project ../m365-bot-service/M365BotService.csproj

using System.Collections;
using Aspire.Hosting.Yarp.Transforms;
using Microsoft.Extensions.Logging;

var builder = DistributedApplication.CreateBuilder(args);

var tenantId = builder.AddParameterFromConfiguration("tenant", "Azure:TenantId");
var existingFoundryName = builder.AddParameter("existingFoundryName")
    .WithDescription("The name of the existing Azure Foundry resource.");
var existingFoundryResourceGroup = builder.AddParameter("existingFoundryResourceGroup")
    .WithDescription("The resource group of the existing Azure Foundry resource.");

var foundry = builder.AddAzureAIFoundry("foundry")
    .AsExisting(existingFoundryName, existingFoundryResourceGroup);

tenantId.WithParentRelationship(foundry);
existingFoundryName.WithParentRelationship(foundry);
existingFoundryResourceGroup.WithParentRelationship(foundry);

#pragma warning disable ASPIRECOSMOSDB001
var cosmos = builder.AddAzureCosmosDB("cosmos-db")
    .RunAsPreviewEmulator(
        emulator =>
        {
            emulator.WithDataExplorer();
            emulator.WithLifetime(ContainerLifetime.Persistent);
        });
        
var db = cosmos.AddCosmosDatabase("db");
var sessions = db.AddContainer("sessions", "/conversationId");
var conversations = db.AddContainer("conversations", "/conversationId");

var restaurantAgent = builder.AddProject("restaurantagent", "../restaurant-agent/RestaurantAgent.csproj")
    .WithHttpHealthCheck("/health")
    .WithReference(foundry).WaitFor(foundry)
    .WithReference(sessions).WaitFor(sessions)
    .WithReference(conversations).WaitFor(conversations)
    .WithEnvironment("AZURE_TENANT_ID", tenantId)
    .WithUrls((e) =>
    {
        e.Urls.Add(new() { Url = "/agenta2a/v1/card", DisplayText = "🤖Restaurant Agent A2A Card", Endpoint = e.GetEndpoint("https") });
    });

var geocodingMcpServer = builder.AddProject("geocodingmcpserver", "../geocoding-mcp-server/GeocodingMcpServer.csproj")
    .WithHttpHealthCheck("/health");

var activitiesAgent = builder.AddProject("activitiesagent", "../activities-agent/ActivitiesAgent.csproj")
    .WithHttpHealthCheck("/health")
    .WithReference(foundry).WaitFor(foundry)
    .WithReference(sessions).WaitFor(sessions)
    .WithReference(conversations).WaitFor(conversations)
    .WithReference(geocodingMcpServer).WaitFor(geocodingMcpServer)
    .WithEnvironment("AZURE_TENANT_ID", tenantId)
    .WithUrls((e) =>
    {
        e.Urls.Add(new() { Url = "/agenta2a/v1/card", DisplayText = "🎭Activities Agent A2A Card", Endpoint = e.GetEndpoint("https") });
    });

var accommodationAgent = builder.AddProject("accommodationagent", "../accommodation-agent/AccommodationAgent.csproj")
    .WithHttpHealthCheck("/health")
    .WithReference(foundry).WaitFor(foundry)
    .WithReference(sessions).WaitFor(sessions)
    .WithReference(conversations).WaitFor(conversations)
    .WithReference(geocodingMcpServer).WaitFor(geocodingMcpServer)
    .WithEnvironment("AZURE_TENANT_ID", tenantId)
    .WithUrls((e) =>
    {
        e.Urls.Add(new() { Url = "/agenta2a/v1/card", DisplayText = "🏨Accommodation Agent A2A Card", Endpoint = e.GetEndpoint("https") });
    });

var orchestratorAgent = builder.AddProject("orchestratoragent", "../orchestrator-agent/OrchestratorAgent.csproj")
    .WithHttpHealthCheck("/health")
    .WithReference(foundry).WaitFor(foundry)
    .WithReference(sessions).WaitFor(sessions)
    .WithReference(conversations).WaitFor(conversations)
    .WithReference(restaurantAgent).WaitFor(restaurantAgent)
    .WithReference(activitiesAgent).WaitFor(activitiesAgent)
    .WithReference(accommodationAgent).WaitFor(accommodationAgent)
    .WithEnvironment("AZURE_TENANT_ID", tenantId)
    .WithUrls((e) =>
    {
        e.Urls.Add(new() { Url = "/agenta2a/v1/card", DisplayText = "🤖Orchestrator Agent A2A Card", Endpoint = e.GetEndpoint("https") });
    });

var frontend = builder.AddViteApp("frontend", "../frontend")
    .WithReference(orchestratorAgent).WaitFor(orchestratorAgent)
    .WithUrls((e) =>
    {
        e.Urls.Clear();
        e.Urls.Add(new() { Url = "/", DisplayText = "💬City Assistant", Endpoint = e.GetEndpoint("http") });
    });

var botService = builder.AddProject("m365botservice", "../m365-bot-service/M365BotService.csproj")
    .WithReference(orchestratorAgent).WaitFor(orchestratorAgent)
    .WithHttpEndpoint(name: "http")
    .WithHttpHealthCheck("/health");

var playground = builder.AddPlayground("Microsoft-365-Agent-Playground", channel: Microsoft365AgentChannel.Emulator)
    .WaitFor(botService)
    .WithBotService(botService);

if (builder.ExecutionContext.IsPublishMode)
{
    builder.AddYarp("yarp")
        .WithExternalHttpEndpoints()
        .WithConfiguration(yarp =>
        {
            yarp.AddRoute("/agent/{**catch-all}", orchestratorAgent)
                .WithTransformPathPrefix("/agent");
        })
        .PublishWithStaticFiles(frontend);
}

builder.Build().Run();
    
public class Microsoft365AgentSDKResource(string name, string command, string workingDirectory, string[] args)
    : ExecutableResource(name, command, workingDirectory), IResourceWithServiceDiscovery
{
    public string[] Args { get; } = args;
}

public static class M365AgentSDKAppHostingExtension
{
    public static IResourceBuilder<Microsoft365AgentSDKResource> AddPlayground(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name = "Microsoft-365-Agent-Playground",
        Microsoft365AgentChannel channel = Microsoft365AgentChannel.Emulator)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var workingDirectory = PathNormalizer.NormalizePathForCurrentPlatform(builder.AppHostDirectory);
        var npxCommand = OperatingSystem.IsWindows() ? "npx.cmd" : "npx";
        var sdkResource = new Microsoft365AgentSDKResource(
            name, npxCommand, workingDirectory, ["--yes", "-D", "@microsoft/m365agentsplayground"]);

        string channelArgs = channel switch
        {
            Microsoft365AgentChannel.Emulator => "emulator",
            Microsoft365AgentChannel.WebChat => "webchat",
            Microsoft365AgentChannel.Teams => "msteams",
            Microsoft365AgentChannel.DirectLine => "directline",
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, null)
        };

        var resource = builder.AddResource(sdkResource)
            .WithArgs(context =>
            {
                foreach (var arg in sdkResource.Args)
                {
                    context.Args.Add(arg);
                }
            }).WithEnvironment(context =>
            {
                context.EnvironmentVariables["DEFAULT_CHANNEL_ID"] = channelArgs;
            });

        return resource;
    }

    public static IResourceBuilder<Microsoft365AgentSDKResource> WithBotService(
        this IResourceBuilder<Microsoft365AgentSDKResource> builder,
        IResourceBuilder<IResourceWithEndpoints> botService)
    {
        ArgumentNullException.ThrowIfNull(botService);

        return builder.WithEnvironment(context =>
        {
            var endpoint = botService.GetEndpoint("http").Url;
            context.EnvironmentVariables["BOT_ENDPOINT"] = endpoint + "/api/messages";
        });
    }
}

public enum Microsoft365AgentChannel
{
    Emulator = 1,
    WebChat = 2,
    Teams = 4,
    DirectLine = 8,
}

internal static class PathNormalizer
{
    public static string NormalizePathForCurrentPlatform(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        // Fix slashes
        path = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

        return Path.GetFullPath(path);
    }
}
