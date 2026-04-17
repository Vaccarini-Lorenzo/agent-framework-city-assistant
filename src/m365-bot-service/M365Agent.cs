// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI.A2A;

internal sealed class M365Agent : AgentApplication
{
    private readonly string? _welcomeMessage;
    private readonly A2AAgent _orchestratorA2AAgent;

    public M365Agent(
        A2AAgent orchestratorA2AAgent,
        AgentApplicationOptions options) : base(options)
    {
        this._orchestratorA2AAgent = orchestratorA2AAgent ?? throw new ArgumentNullException(nameof(orchestratorA2AAgent));
        this._welcomeMessage = "Login successful";
        this.OnConversationUpdate(ConversationUpdateEvents.MembersAdded, this.WelcomeMessageAsync);
        this.OnActivity(ActivityTypes.Message, this.ForwardToOrchestrator, rank: RouteRank.Last);
    }

    private async Task ForwardToOrchestrator(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        string conversationId = turnContext.Activity.Conversation.Id;
        string userMessage = turnContext.Activity.Text ?? string.Empty;

        Console.WriteLine($"Received message from user: {userMessage}");
        Console.WriteLine($"Conversation ID: {conversationId}");

        // Propagate session to maintain the conversation context in the orchestrator agent.
        // The state is kept orchestrator-side
        var session = await _orchestratorA2AAgent.CreateSessionAsync(conversationId);

        AgentResponse response = await this._orchestratorA2AAgent.RunAsync(new ChatMessage(ChatRole.User, userMessage), cancellationToken: cancellationToken, session: session);
        await turnContext.SendActivityAsync(MessageFactory.Text(response.Text), cancellationToken);
    }

    private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(this._welcomeMessage))
        {
            return;
        }

        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text(this._welcomeMessage), cancellationToken);
            }
        }
    }

}
