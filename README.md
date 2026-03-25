# City Assistant - Agent Framework Demo

A multi-agent application built with Microsoft Agent Framework, featuring restaurant and accommodation recommendation systems with orchestrated agents.

## Architecture

The application consists of six main components:

1. **Restaurant Agent** - A specialized agent that can search and recommend restaurants by category or keywords
2. **Accommodation Agent** - A specialized agent that can search and recommend accommodations (hotels, B&Bs, hostels) based on multiple criteria with LLM-based reranking
3. **Geocoding MCP Server** - A Model Context Protocol server that provides geocoding services (address/landmark to coordinates conversion) shared across agents
4. **Orchestrator Agent** - An orchestrator that uses the restaurant and accommodation agents as tools via A2A (Agent-to-Agent) communication
5. **Frontend** - A React-based chat interface that communicates with the orchestrator via A2A protocol
6. **M365 Bot Service** - An alternative frontend that connects the orchestrator to Microsoft 365 channels (Teams, Emulator, WebChat, DirectLine) via Azure Bot Service

### Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                          Frontend (React)                       │
│                                                                 │
│  • A2A JavaScript SDK (@a2a-js/sdk)                             │
│  • Streaming chat interface                                     │
│  • Theme support & session management                           │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             │ A2A Protocol
                             │ /agenta2a/v1/*
                             │ (Agent Card, Run, Stream)
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                     Orchestrator Agent (.NET)                   │
│                                                                 │
│  • Receives user requests via A2A                               │
│  • Maintains conversation context (contextId)                   │
│  • Invokes Restaurant & Accommodation Agents as tools           │
│  • Stores conversation history in Cosmos DB                     │
└───────┬──────────────────┬─────────────────┬────────────────────┘
        │                  │                 │         ▲
        │ A2A Protocol     │ A2A Protocol    │ Azure   │ A2A Protocol
        │ /agenta2a/v1/*   │ /agenta2a/v1/*  │ Cosmos  │ /agenta2a/v1/*
        │ (Agent-to-Agent) │ (Agent-to-Agent)│ DB      │ (Agent-to-Agent)
        ▼                  ▼                 ▼         │
┌───────────────────┐  ┌───────────────────────┐  ┌───────────────────────────┐
│ Restaurant Agent  │  │ Accommodation Agent   │  │   M365 Bot Service (.NET) │
│      (.NET)       │  │       (.NET)          │  │                           │
│                   │  │                       │  │ • /api/messages endpoint  │
│ • Search tools    │  │ • Multi-criteria      │  │ • Azure Bot Service       │
│ • Category filter │  │   search (rating,     │  │ • Teams, Emulator,        │
│ • Mock data       │  │   location, amenities,│  │   WebChat, DirectLine     │
│ • A2A endpoint    │  │   price, type)        │  │ • Forwards to Orchestrator│
│                   │  │ • LLM-based reranking │  │   via A2A                 │
│                   │  │ • Mock data           │  └─────────────┬─────────────┘
│                   │  │ • A2A endpoint        │                │
└─────────┬─────────┘  └─────────┬─────────────┘                │
          │                      │                   ┌──────────┴──────────┐
          │                      │ MCP Protocol      │  M365 Channels      │
          │                      │ (HTTP)            │  (Teams, Emulator,  │
          │                      ▼                   │   WebChat, etc.)    │
          │            ┌────────────────────────┐    └─────────────────────┘
          │            │ Geocoding MCP Server   │
          │            │      (.NET)            │
          │            │                        │
          │            │ • geocode_location     │
          │            │   tool                 │
          │            │ • Mock Rome landmarks  │
          │            │ • HTTP/MCP endpoints   │
          │            └────────────────────────┘
          │
          └──────────┬───────────┘
                     │
                     ▼
       ┌────────────────────────────┐
       │     Cosmos DB              │
       │                            │
       │  • Conversation threads    │
       │  • Message history         │
       │  • Context persistence     │
       └────────────────────────────┘

Data Flow:
1. User sends message via Frontend → Orchestrator (A2A)
   OR via M365 Channel → (ONLY IF NO EMULATOR) Azure Bot Service → M365 Bot Service module → Orchestrator (A2A)
2. Orchestrator determines which agent(s) are needed
3. If needed: Orchestrator → Restaurant/Accommodation Agent (A2A as tool)
4. Accommodation Agent → Geocoding MCP Server (MCP protocol for location lookup)
5. Agent searches mock data (accommodation agent applies LLM reranking)
6. Orchestrator streams response back to Frontend (A2A)
   OR Orchestrator → Bot Service → M365 Channel
7. All agents persist conversation state to Cosmos DB
```

## Prerequisites

- .NET 10 SDK
- Node.js 18+ and npm
- Azure AI Inference (Foundry) connection
- Azure Cosmos DB instance

## Run the sample

> This sample requires latest .Net 10 Preview SDK (RC2) and Python 3.11+ installed on your machine.

To allow Aspire to create or reference existing resources on Azure (e.g. Foundry), you need to configure Azure settings in the [appsettings.json](./src/aspire/appsettings.json) file:

```json
{
  "Azure": {
    "TenantId": "<YOUR-TENANT-ID>",
    "SubscriptionId": "<YOUR-SUBSCRIPTION-ID>",
    "AllowResourceGroupCreation": false,
    "Location": "<YOUR-LOCATION>",
    "CredentialSource": "AzureCli"
  },
  "Parameters": {
    "existingFoundryName": "<YOUR-FOUNDRY-NAME>",
    "existingFoundryResourceGroup": "<YOUR-RESOURCE-GROUP-NAME>"
  }
}
```

| Property | Description |
|---|---|
| `TenantId` | Your Azure Active Directory tenant ID |
| `SubscriptionId` | Your Azure subscription ID |
| `AllowResourceGroupCreation` | Set to `true` if you want Aspire to create a new resource group |
| `Location` | Azure region for your resources (e.g. `eastus`, `italynorth`) |
| `CredentialSource` | Authentication method — use `AzureCli` after running `az login` |
| `existingFoundryName` | The name of your existing Azure AI Foundry resource |
| `existingFoundryResourceGroup` | The resource group where the Foundry resource is deployed |

Use [aspire cli](https://learn.microsoft.com/en-us/dotnet/aspire/cli/install) to run the sample.

Powershell:
```bash
iex "& { $(irm https://aspire.dev/install.ps1) } -InstallExtension"

aspire run
```

Bash:
```bash
curl -sSL https://aspire.dev/install.sh -o aspire-install.sh && ./aspire-install.sh

aspire run
```

To ease the debug experience, you can use the [Aspire extension for Visual Studio Code](https://marketplace.visualstudio.com/items?itemName=microsoft-aspire.aspire-vscode#:~:text=The%20Aspire%20VS%20Code%20extension,directly%20from%20Visual%20Studio%20Code.).

## Features

### Restaurant Agent
- Search restaurants by category (vegetarian, pizza, japanese, mexican, french, indian, steakhouse)
- Search restaurants by keywords
- Get all available restaurants
- A2A endpoint at `/agenta2a`
- OpenAI-compatible endpoints for testing

### Accommodation Agent
- **Multi-criteria search** with the following filters:
  - User rating (1-5 scale)
  - Location (city name or proximity to coordinates)
  - Amenities (parking, wifi, breakfast, room-service, gym, spa, restaurant, pool, etc.)
  - Price per night (in euros)
  - Accommodation type (Hotel, BedAndBreakfast, Hostel, Apartment, Resort, Guesthouse, Motel, Villa, Boutique)
- **Geocoding via MCP** - Uses the shared Geocoding MCP Server to convert addresses/landmarks to coordinates
  - Communicates via Model Context Protocol (MCP) over HTTP
  - Known locations: Colosseum, Vatican, Pantheon, Trevi Fountain, Rome, Latina, etc.
  - Smart fallback with Rome city center coordinates for unknown locations
- **LLM-based reranking** using pointwise scoring (1-10 scale)
  - Parallel processing with configurable MAXDOP (default: 3)
  - Returns only highly relevant results (score > 6)
  - Detailed grading criteria and evaluation process
- Semantically rich accommodation descriptions for optimal reranking
- A2A endpoint at `/agenta2a`
- OpenAI-compatible endpoints for testing

### Geocoding MCP Server
- **Model Context Protocol (MCP) compliant** server for geocoding services
- Exposes `geocode_location` tool via MCP protocol
- Mock geocoding data for Rome landmarks and cities:
  - Rome landmarks: Colosseum, Vatican, Pantheon, Trevi Fountain, Spanish Steps, Trastevere, etc.
  - Cities: Rome, Latina
  - Areas: Downtown Rome, Termini Station
- Returns coordinates in latitude/longitude format
- Fallback to Rome city center for unknown locations
- HTTP-based MCP transport
- Can be consumed by any MCP-compatible client or agent
- Shared across multiple agents in the system
- Health check endpoint at `/health`
- MCP endpoints at `/mcp/v1/*`

### Orchestrator Agent
- Orchestrates calls to the restaurant and accommodation agents
- Maintains conversation history via Cosmos DB
- Exposes A2A endpoint at `/agenta2a` for frontend communication
- Integrates with specialized agents via A2A protocol
- Uses contextId for conversation management

### Frontend
- Clean, modern chat interface
- Streaming responses via A2A JavaScript SDK
- Theme support (light/dark/system)
- Session management with conversation history using contextId
- Communicates with orchestrator via A2A protocol


## M365 Bot Service module

The m365 bot service module receives messages through the `/api/messages` endpoint, forwards user queries to the orchestrator agent via A2A protocol, and streams responses back to the caller.

### Option 1: Local Demo with Agents Playground (Emulator)

For demo purposes, the Aspire host includes an extension that automatically launches the **Microsoft 365 Agents Playground** (emulator). In this scenario, the emulator directly sends messages to the M365 Bot Service `/api/messages` endpoint: no Azure Bot Service registration is required.

This is configured in `apphost.cs`:

```csharp
var playground = builder.AddPlayground("Microsoft-365-Agent-Playground", channel: Microsoft365AgentChannel.Emulator)
    .WaitFor(botService)
    .WithBotService(botService);
```

```
┌─────────────────────────┐       POST /api/messages        ┌─────────────────────────┐
│  Agents Playground      │ ─────────────────────────────▶  │  M365 Bot Service       │
│  (Emulator)             │ ◀─────────────────────────────  │  (.NET)                 │
│                         │       Response                  │                         │
└─────────────────────────┘                                 └────────────┬────────────┘
                                                                         │
                                                                         │ A2A Protocol
                                                                         ▼
                                                            ┌─────────────────────────┐
                                                            │  Orchestrator Agent     │
                                                            └─────────────────────────┘
```

> **Note:** In this mode, JWT validation is intentionally disabled to allow the demo to work out of the box (clone & run). See the [Security Disclaimer](#️-security-disclaimer) below.

### Option 2: Azure Bot Service Instance

For production or integration with real M365 channels (Teams, WebChat, DirectLine), you connect through an **Azure Bot Service** instance. In this scenario, the M365 channel (e.g., Teams) sends the message to Azure Bot Service, which then forwards it (with a valid JWT token) to the M365 Bot Service `/api/messages` endpoint.

```
┌─────────────────┐       ┌─────────────────────┐    POST /api/messages    ┌─────────────────────────┐
│  M365 Channel   │ ────▶ │  Azure Bot Service  │ ────────(+JWT)────────▶  │  M365 Bot Service       │
│  (Teams, etc.)  │ ◀──── │  (Cloud)            │ ◀──────────────────────  │  (.NET)                 │
└─────────────────┘       └─────────────────────┘       Response           └────────────┬────────────┘
                                                                                        │
                                                                                        │ A2A Protocol
                                                                                        ▼
                                                                           ┌─────────────────────────┐
                                                                           │  Orchestrator Agent     │
                                                                           └─────────────────────────┘
```

To set this up:

1. **Fill the `appsettings.json`** in `src/m365-bot-service/`:

    ```json
    {
      "TokenValidation": {
        // IMPORTANT!
        "Enabled": true,
        "Audiences": [
          "<BOT_APP_CLIENT_ID>"
        ],
        "TenantId": "<AZURE_AD_TENANT_ID>"
      },
      "AgentApplication": {
        "StartTypingTimer": true,
        "RemoveRecipientMention": false,
        "NormalizeMentions": false
      },
      "Connections": {
        "ServiceConnection": {
          "Settings": {
            "AuthType": "ClientSecret",
            "ClientId": "<BOT_APP_CLIENT_ID>",
            "ClientSecret": "<BOT_APP_CLIENT_SECRET>",
            "AuthorityEndpoint": "https://login.microsoftonline.com/<AZURE_AD_TENANT_ID>",
            "Scopes": [
              "https://api.botframework.com/.default"
            ]
          }
        }
      },
      "ConnectionsMap": [
        {
          "ServiceUrl": "*",
          "Connection": "ServiceConnection"
        }
      ],
      "Logging": {
        "LogLevel": {
          "Default": "Information",
          "Microsoft.AspNetCore": "Warning"
        }
      }
    }
    ```

    Replace all `<BOT_APP_CLIENT_ID>`, `<BOT_APP_CLIENT_SECRET>`, and `<AZURE_AD_TENANT_ID>` placeholders with your actual Azure Bot registration values.

2. **Enable JWT authorization** by uncommenting `.RequireAuthorization()` in `src/m365-bot-service/Extensions/RouteExtension.cs`:

    ```csharp
    app.MapPost("/api/messages",
                async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
                {
                    await adapter.ProcessAsync(request, response, agent, cancellationToken);
                })
                .RequireAuthorization();
    ```

3. **(Optional)** Comment out the playground resource in `apphost.cs` if you no longer need the local emulator.

### ⚠️ Security Disclaimer

> **AS IS, the M365 Bot Service bypasses JWT token validation.** The `.RequireAuthorization()` call is intentionally commented out in `RouteExtension.cs` to allow the demo to work out of the box with the Agents Playground emulator, without any Azure Bot Service registration (clone & run).
>
> **This means any request with an empty or missing `Authorization` header will be accepted and processed by the bot.**
>
> **For any scenario beyond local demos, you MUST:**
> 1. **Uncomment `.RequireAuthorization()`** in `RouteExtension.cs`
> 2. **Set `"Enabled": true`** in the `TokenValidation` section of `appsettings.json` to enforce JWT validation
>
> Provide valid `TokenValidation` and `Connections` settings in `appsettings.json`. Failure to do so exposes the bot endpoint to unauthorized access.


## Mock Data

### Restaurant Agent
The restaurant agent includes mock data for 11 restaurants across various categories:
- 3 Vegetarian restaurants
- 3 Pizza places
- 5 Other cuisines (Japanese, Mexican, French, Indian, Steakhouse)

### Accommodation Agent
The accommodation agent includes mock data for 12 accommodations in Rome and Latina:
- 5 Hotels (ranging from budget to luxury, €45-€450 per night)
- 3 Bed & Breakfasts (cozy options, €65-€80 per night)
- 1 Hostel (budget-friendly, €30 per night)
- 1 Boutique hotel (premium location, €280 per night)
- 2 Hotels in Latina (€85-€110 per night)

Each accommodation includes:
- Detailed description with location context, amenities, and target audience
- User ratings (3.8-4.9 out of 5)
- GPS coordinates for proximity search
- Complete address information
- List of amenities (parking, wifi, breakfast, gym, spa, pool, etc.)
- Accommodation type (enum-based)

All data is hardcoded in service classes and doesn't require external data sources.

## API Endpoints

### Restaurant Agent
- `GET /agenta2a/v1/card` - A2A agent card (metadata and capabilities)
- `POST /agenta2a/v1/run` - A2A endpoint for agent-to-agent communication
- `POST /agenta2a/v1/stream` - A2A streaming endpoint
- `POST /v1/chat/completions` - OpenAI-compatible chat endpoint (for testing)
- `GET /health` - Health check endpoint

### Accommodation Agent
- `GET /agenta2a/v1/card` - A2A agent card (metadata and capabilities)
- `POST /agenta2a/v1/run` - A2A endpoint for agent-to-agent communication
- `POST /agenta2a/v1/stream` - A2A streaming endpoint
- `POST /v1/chat/completions` - OpenAI-compatible chat endpoint (for testing)
- `GET /health` - Health check endpoint

### Geocoding MCP Server
- `POST /mcp/v1/initialize` - Initialize MCP session
- `GET /mcp/v1/tools/list` - List available MCP tools
- `POST /mcp/v1/tools/call` - Call an MCP tool (e.g., geocode_location)
- `GET /health` - Health check endpoint

### Orchestrator Agent
- `GET /agenta2a/v1/card` - A2A agent card (metadata and capabilities)
- `POST /agenta2a/v1/run` - A2A endpoint for frontend and agent communication
- `POST /agenta2a/v1/stream` - A2A streaming endpoint for real-time responses
- `GET /health` - Health check endpoint

### M365 Bot Service
- `POST /api/messages` - Bot Framework messaging endpoint (receives messages from Agents Playground or Azure Bot Service)
- `GET /health` - Health check endpoint

### Agents Playground (Emulator)
- `GET /` - Emulator entrypoint


All communication between frontend and orchestrator uses the A2A protocol for standardized message formats, streaming support, and contextId-based conversation management.

The accommodation agent uses the Model Context Protocol (MCP) to communicate with the geocoding server for location-based queries.

## Development

### Project Structure

```
src/
├── service-defaults/          # Shared Aspire service configuration
├── shared-services/           # Shared services (Cosmos session store)
├── restaurant-agent/          # Restaurant recommendation agent
│   ├── Models/               # Data models
│   ├── Services/             # Business logic and storage
│   └── Tools/                # Agent tools/functions
├── accommodation-agent/       # Accommodation recommendation agent
│   ├── Models/               # Data models (Accommodation, AccommodationType, etc.)
│   ├── Services/             # Business logic (search, reranking, MCP geocoding client)
│   └── Tools/                # Agent tools/functions
├── geocoding-mcp-server/     # Geocoding MCP server
│   ├── Tools/                # MCP tools (geocode_location)
│   └── Program.cs            # MCP server setup
├── orchestrator-agent/       # Orchestrator agent
│   └── Program.cs            # Main orchestrator logic
├── m365-bot-service/         # M365 Bot Service (Teams, Emulator, WebChat)
│   ├── Extensions/           # Route and service extensions
│   ├── Services/             # A2A client for orchestrator communication
│   └── Program.cs            # Bot service setup
├── frontend/                 # React frontend
│   └── src/
│       ├── Chat.tsx         # Main chat component
│       └── ...
└── aspire/                   # Aspire orchestration
    └── apphost.cs           # Single-file Aspire host
```

### Building

```bash
# Build all projects
dotnet build

# Build specific project
cd src/restaurant-agent && dotnet build
cd src/orchestrator-agent && dotnet build
```

### Testing the Agents via A2A

You can test the agents' A2A endpoints directly:

```bash
# Get the agent card to see capabilities
curl https://localhost:5197/agenta2a/v1/card  # Orchestrator
curl https://localhost:5196/agenta2a/v1/card  # Restaurant Agent
curl https://localhost:5198/agenta2a/v1/card  # Accommodation Agent

# Send a message to the orchestrator
# Note: messageId should be a unique UUID for each message
# Note: contextId maintains conversation continuity across requests
curl -X POST https://localhost:5197/agenta2a/v1/run \
  -H "Content-Type: application/json" \
  -d '{
    "message": {
      "messageId": "550e8400-e29b-41d4-a716-446655440000",
      "role": "user",
      "kind": "message",
      "parts": [
        {
          "kind": "text",
          "text": "Find me a vegetarian restaurant"
        }
      ],
      "contextId": "conversation-abc123"
    }
  }'

# Example: Search for accommodations
curl -X POST https://localhost:5197/agenta2a/v1/run \
  -H "Content-Type: application/json" \
  -d '{
    "message": {
      "messageId": "550e8400-e29b-41d4-a716-446655440001",
      "role": "user",
      "kind": "message",
      "parts": [
        {
          "kind": "text",
          "text": "Find me a hotel near the Colosseum with parking for less than 80€ per night"
        }
      ],
      "contextId": "conversation-abc123"
    }
  }'
```

The frontend uses the `@a2a-js/sdk` package to handle A2A protocol communication, including streaming responses and conversation context management.

## Troubleshooting

### Agent Connection Issues
- Ensure all agents (restaurant, accommodation) are running and accessible
- Check that environment variables for agent URLs are set correctly in orchestrator
  - `services__restaurantagent__https__0` or `services__restaurantagent__http__0`
  - `services__accommodationagent__https__0` or `services__accommodationagent__http__0`
- Verify SSL certificate if using HTTPS in development

### Cosmos DB Connection Issues
- Verify your Cosmos DB connection string is valid
- Ensure the `conversations` container exists or can be created
- Check that your Azure Cosmos DB firewall rules allow your IP

### Frontend Connection Issues
- Verify the proxy configuration in `vite.config.ts`
- Check that orchestrator agent is running on the expected port
- Look for CORS issues in browser console

### LLM Reranking Issues (Accommodation Agent)
- Verify Azure AI Foundry connection is properly configured
- Check that the chat client model (gpt-4.1) is available
- Monitor logs for reranking errors or invalid scores
- Ensure parallel processing limit (MAXDOP) is appropriate for your setup

## License

MIT
