using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using A2A;
using BotService.Configuration;
using Microsoft.Extensions.Options;

namespace BotService.Clients;

internal sealed class OrchestratorA2AClient
{
    private readonly HttpClient _httpClient;
    private readonly OrchestratorA2AOptions _orchestratorOptions;
    private Uri? _cachedMessageEndpointUri;

    public OrchestratorA2AClient(
        HttpClient httpClient,
        IOptions<OrchestratorA2AOptions> orchestratorOptions)
    {
        _httpClient = httpClient;
        _orchestratorOptions = orchestratorOptions.Value;
    }

    public async Task<string> SendUserMessageAsync(
        string conversationId,
        string userMessage,
        CancellationToken cancellationToken)
    {
        Uri messageEndpointUri = await this.ResolveMessageEndpointAsync(cancellationToken);

        JsonRpcMessageSendRequest rpcRequest = new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Parameters = new MessageSendParams
            {
                Message = new A2AMessage
                {
                    MessageId = $"{conversationId}-{Guid.NewGuid():N}",
                    Role = "user",
                    Parts =
                    [
                        new A2ATextPart
                        {
                            Type = "text",
                            Text = userMessage
                        }
                    ]
                }
            }
        };

        string serializedRequest = JsonSerializer.Serialize(rpcRequest);

        using HttpRequestMessage httpRequest = new(HttpMethod.Post, messageEndpointUri)
        {
            Content = new StringContent(serializedRequest, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using HttpResponseMessage httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
        string responseBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            return $"Orchestrator call failed with status {(int)httpResponse.StatusCode}.";
        }

        return TryExtractText(responseBody) ?? "No response from orchestrator.";
    }

    private async Task<Uri> ResolveMessageEndpointAsync(CancellationToken cancellationToken)
    {
        if (_cachedMessageEndpointUri is not null)
        {
            return _cachedMessageEndpointUri;
        }

        Uri orchestratorBaseUri = new(_orchestratorOptions.BaseUrl);
        A2ACardResolver cardResolver = new(
            orchestratorBaseUri,
            _httpClient,
            _orchestratorOptions.AgentCardPath);

        AgentCard agentCard = await cardResolver.GetAgentCardAsync(cancellationToken);

        string serializedAgentCard = JsonSerializer.Serialize(agentCard);
        if (TryFindA2AEndpointInCard(serializedAgentCard, out Uri? discoveredEndpoint))
        {
            _cachedMessageEndpointUri = discoveredEndpoint;
            return _cachedMessageEndpointUri;
        }

        _cachedMessageEndpointUri = new Uri(orchestratorBaseUri, _orchestratorOptions.FallbackMessageEndpointPath.TrimStart('/'));
        return _cachedMessageEndpointUri;
    }

    private static bool TryFindA2AEndpointInCard(string serializedAgentCard, out Uri? endpointUri)
    {
        using JsonDocument cardDocument = JsonDocument.Parse(serializedAgentCard);

        if (TryFindA2AEndpointRecursive(cardDocument.RootElement, out string? endpointValue) &&
            Uri.TryCreate(endpointValue, UriKind.Absolute, out Uri? absoluteUri))
        {
            endpointUri = absoluteUri;
            return true;
        }

        endpointUri = null;
        return false;
    }

    private static bool TryFindA2AEndpointRecursive(JsonElement node, out string? endpointValue)
    {
        endpointValue = null;

        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in node.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        string? currentValue = property.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(currentValue) &&
                            currentValue.Contains("agenta2a", StringComparison.OrdinalIgnoreCase))
                        {
                            endpointValue = currentValue;
                            return true;
                        }
                    }

                    if (TryFindA2AEndpointRecursive(property.Value, out endpointValue))
                    {
                        return true;
                    }
                }
                break;

            case JsonValueKind.Array:
                foreach (JsonElement item in node.EnumerateArray())
                {
                    if (TryFindA2AEndpointRecursive(item, out endpointValue))
                    {
                        return true;
                    }
                }
                break;
        }

        return false;
    }

    private static string? TryExtractText(string jsonRpcResponseBody)
    {
        using JsonDocument responseDocument = JsonDocument.Parse(jsonRpcResponseBody);

        if (!responseDocument.RootElement.TryGetProperty("result", out JsonElement resultNode))
        {
            return null;
        }

        if (TryReadTextParts(resultNode, "parts", out string? directText))
        {
            return directText;
        }

        if (resultNode.TryGetProperty("message", out JsonElement messageNode) &&
            TryReadTextParts(messageNode, "parts", out string? messageText))
        {
            return messageText;
        }

        if (resultNode.TryGetProperty("artifacts", out JsonElement artifactsNode) &&
            artifactsNode.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement artifact in artifactsNode.EnumerateArray())
            {
                if (TryReadTextParts(artifact, "parts", out string? artifactText))
                {
                    return artifactText;
                }
            }
        }

        return null;
    }

    private static bool TryReadTextParts(JsonElement containerNode, string partsPropertyName, out string? extractedText)
    {
        extractedText = null;

        if (!containerNode.TryGetProperty(partsPropertyName, out JsonElement partsNode) ||
            partsNode.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        List<string> textParts = [];

        foreach (JsonElement partNode in partsNode.EnumerateArray())
        {
            if (partNode.TryGetProperty("type", out JsonElement typeNode) &&
                string.Equals(typeNode.GetString(), "text", StringComparison.OrdinalIgnoreCase) &&
                partNode.TryGetProperty("text", out JsonElement textNode))
            {
                string? partText = textNode.GetString();
                if (!string.IsNullOrWhiteSpace(partText))
                {
                    textParts.Add(partText);
                }
            }
        }

        if (textParts.Count == 0)
        {
            return false;
        }

        extractedText = string.Join(Environment.NewLine, textParts);
        return true;
    }

    private sealed class JsonRpcMessageSendRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; init; } = "2.0";

        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("method")]
        public string Method { get; init; } = "message/send";

        [JsonPropertyName("params")]
        public MessageSendParams Parameters { get; init; } = new();
    }

    private sealed class MessageSendParams
    {
        [JsonPropertyName("message")]
        public A2AMessage Message { get; init; } = new();
    }

    private sealed class A2AMessage
    {
        [JsonPropertyName("messageId")]
        public string MessageId { get; init; } = string.Empty;

        [JsonPropertyName("role")]
        public string Role { get; init; } = "user";

        [JsonPropertyName("parts")]
        public List<A2ATextPart> Parts { get; init; } = [];
    }

    private sealed class A2ATextPart
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "text";

        [JsonPropertyName("text")]
        public string Text { get; init; } = string.Empty;
    }
}