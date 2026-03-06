// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using A2A;
using BotService.Clients;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

internal sealed class M365Agent : AgentApplication
{
    private readonly string? _welcomeMessage;
    private readonly AIAgent _orchestratorA2AAgent;

    public M365Agent(
        AIAgent orchestratorA2AAgent,
        AgentApplicationOptions options) : base(options)
    {
        this._orchestratorA2AAgent = orchestratorA2AAgent ?? throw new ArgumentNullException(nameof(orchestratorA2AAgent));
        _welcomeMessage = "Login successful! You can now interact with the agent.";
        this.OnConversationUpdate(ConversationUpdateEvents.MembersAdded, this.WelcomeMessageAsync);
        this.OnActivity(ActivityTypes.Message, this.MessageActivityAsync, rank: RouteRank.Last);
    }

    private async Task MessageActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        string conversationId = turnContext.Activity.Conversation.Id;
        string userMessage = turnContext.Activity.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(userMessage))
        {
            await turnContext.SendActivityAsync(MessageFactory.Text("Please send a non-empty message."), cancellationToken);
            return;
        }

        AgentResponse response = await this._orchestratorA2AAgent.RunAsync(new ChatMessage(ChatRole.User, userMessage), cancellationToken: cancellationToken);

        // string orchestratorResponse = await _orchestratorA2AClient.SendUserMessageAsync(
        //     conversationId,
        //     userMessage,
        //     cancellationToken);

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
