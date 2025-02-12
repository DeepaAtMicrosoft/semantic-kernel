﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Experimental.Orchestration;
using Microsoft.SemanticKernel.Orchestration;

#pragma warning disable SKEXP0001

namespace SemanticKernel.Experimental.Orchestration.Flow.IntegrationTests;

public sealed class CollectEmailPlugin
{
    private const string Goal = "Collect email from user";

    private const string EmailRegex = @"^([\w\.\-]+)@([\w\-]+)((\.(\w){2,3})+)$";

    private const string SystemPrompt =
        $@"I am AI assistant and will only answer questions related to collect email.
The email should conform the regex: {EmailRegex}

If I cannot answer, say that I don't know.
Do not expose the regex unless asked.
";

    private readonly IChatCompletion _chat;

    private int MaxTokens { get; set; } = 256;

    private readonly AIRequestSettings _chatRequestSettings;

    public CollectEmailPlugin(Kernel kernel)
    {
        this._chat = kernel.GetService<IChatCompletion>();
        this._chatRequestSettings = new OpenAIRequestSettings
        {
            MaxTokens = this.MaxTokens,
            StopSequences = new List<string>() { "Observation:" },
            Temperature = 0
        };
    }

    [SKFunction]
    [Description("Useful to assist in configuration of email address, must be called after email provided")]
    [SKName("ConfigureEmailAddress")]
    public async Task<string> CollectEmailAsync(
        [SKName("email_address")] [Description("The email address provided by the user, pass no matter what the value is")]
        string email,
        SKContext context)
    {
        var chat = this._chat.CreateNewChat(SystemPrompt);
        chat.AddUserMessage(Goal);

        ChatHistory? chatHistory = context.GetChatHistory();
        if (chatHistory?.Any() ?? false)
        {
            chat.AddRange(chatHistory);
        }

        if (!string.IsNullOrEmpty(email) && IsValidEmail(email))
        {
            context.Variables["email_address"] = email;

            return "Thanks for providing the info, the following email would be used in subsequent steps: " + email;
        }

        // invalid email, prompt user to provide a valid email
        context.Variables["email_address"] = string.Empty;
        context.PromptInput();
        return await this._chat.GenerateMessageAsync(chat, this._chatRequestSettings).ConfigureAwait(false);
    }

    private static bool IsValidEmail(string email)
    {
        // check using regex
        var regex = new Regex(EmailRegex);
        return regex.IsMatch(email);
    }
}
