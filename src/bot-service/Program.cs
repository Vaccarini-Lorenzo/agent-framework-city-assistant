// Copyright (c) Microsoft. All rights reserved.
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
// Add Aspire services
builder.AddServiceDefaults();
// Add IHttpClient and IHttpClientFactory to the service collection.
builder.Services.AddHttpClient();
// Add AgentApplicationOptions from appsettings section "AgentApplication".
builder.AddAgentApplicationOptions();
// Add the AgentApplication, which contains the logic for responding to incoming activities.
builder.AddAgent<M365Agent>();
// Use in-memory storage for conversation and user state.
builder.Services.AddSingleton<IStorage, MemoryStorage>();
// Configure the HTTP request pipeline.
// Add AspNet token validation for Azure Bot Service and Entra.  Authentication is
// configured in the appsettings.json "TokenValidation" section.
builder.Services.AddControllers();
// Add logic to validate incoming tokens from the Bot Service.
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);
// Add A2A orchestrator agent singleton
builder.Services.AddA2AAgents();

WebApplication app = builder.Build();
// Enable AspNet authentication and authorization
app.UseAuthentication();
app.UseAuthorization();
// Configure health endpoints
app.MapDefaultEndpoints();
// Configure routes including /api/messages
app.ConfigureRoutes();
app.Run();
