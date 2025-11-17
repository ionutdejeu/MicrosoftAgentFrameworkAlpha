using AgentWebApi.Interfaces;
using AgentWebApi.Configuration;
using AgentWebApi.Constants;
using AgentWebApi.Models;
using AgentWebApi.Stores;
using OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Reflection;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
// Removed SemanticKernel.MongoDB and MongoDB.Driver references: not used in this project
using AgentWebApi.Data;

namespace AgentWebApi.Services;

public class AgentProvider : IAgentProvider
{
    private readonly ChatClientAgent? _geographyAgent;
    private readonly ChatClientAgent? _mathAgent;
    private readonly ChatClientAgent? _orchestratorAgent;
    private readonly OpenAIClient? _chatClientObj;
    private readonly AgentConfiguration _config;
    private readonly string? _openAiKey;
    private readonly ChatHistoryDbContext? _chatHistoryDbContext;
    private readonly ILoggerFactory? _loggerFactory;

    public AgentProvider(
        IOptions<AgentConfiguration> agentConfig,
        ChatHistoryDbContext? chatHistoryDbContext = null,
        ILoggerFactory? loggerFactory = null)
    {
        _config = agentConfig.Value;
        _chatHistoryDbContext = chatHistoryDbContext;
        _loggerFactory = loggerFactory;

        // Initialize OpenAI Chat client using API key from configuration (OpenAIKey)
        _openAiKey = !string.IsNullOrWhiteSpace(_config.OpenAIKey)
            ? _config.OpenAIKey
            : Environment.GetEnvironmentVariable("OPENAI_API_KEY");



        _chatClientObj = new OpenAIClient(_openAiKey);
        // Create agents once during initialization
        _geographyAgent = CreateGeographyAgent();
        _mathAgent = CreateMathAgent();
        _orchestratorAgent = CreateOrchestratorAgent();
    }
     
     

    public AIAgent GetGeographyAgent()
    {
        return _geographyAgent ?? throw new InvalidOperationException("Geography agent is not initialized. Ensure Azure OpenAI deployment and endpoint environment variables are set.");
    }

    public AIAgent GetMathAgent()
    {
        return _mathAgent ?? throw new InvalidOperationException("Math agent is not initialized. Ensure Azure OpenAI deployment and endpoint environment variables are set.");
    }

    public AIAgent GetOrchestratorAgent()
    {
        return _orchestratorAgent ?? throw new InvalidOperationException("Orchestrator agent is not initialized. Ensure Azure OpenAI deployment and endpoint environment variables are set.");
    }


    public AIAgent GetAgent(string agentName)
    {
        return agentName switch
        {
            AgentNames.GeographyAgent => GetGeographyAgent(),
            AgentNames.MathAgent => GetMathAgent(),
            AgentNames.OrchestratorAgent => GetOrchestratorAgent(),
            _ => throw new InvalidOperationException($"{agentName} agent not found or not supported")
        };
    }

    private ChatClientAgent CreateGeographyAgent()
    {
        if (!_config.Agents.TryGetValue(AgentNames.GeographyAgent, out var agentSettings))
        {
            throw new InvalidOperationException($"{AgentNames.GeographyAgent} not found in configuration");
        }

        var geographyOptions = new ChatClientAgentOptions
        {
            Instructions = agentSettings.Instructions,
            Name = agentSettings.Name,
            ChatOptions = new()
            {
                ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.ForJsonSchema<GeographyResponse>()
            }
        };
        return CreateAgentUsingReflection(_chatClientObj, geographyOptions);
    }

    private ChatClientAgent CreateMathAgent()
    {
        if (!_config.Agents.TryGetValue(AgentNames.MathAgent, out var agentSettings))
        {
            throw new InvalidOperationException($"{AgentNames.MathAgent} not found in configuration");
        }

        var mathOptions = new ChatClientAgentOptions
        {
            Instructions = agentSettings.Instructions,
            Name = agentSettings.Name
        };

        return CreateAgentUsingReflection(_chatClientObj, mathOptions);
    }

    private ChatClientAgent CreateOrchestratorAgent()
    {
        if (!_config.Agents.TryGetValue(AgentNames.OrchestratorAgent, out var agentSettings))
        {
            throw new InvalidOperationException($"{AgentNames.OrchestratorAgent} not found in configuration");
        }

        var orchestratorOptions = new ChatClientAgentOptions
        {
            Instructions = agentSettings.Instructions,
            Name = agentSettings.Name,
            ChatMessageStoreFactory = ctx =>
            {
                var logger = _loggerFactory?.CreateLogger<VectorChatMessageStore>();

                // If a ChatHistoryDbContext was provided via DI prefer the SQL-backed store
                if (_chatHistoryDbContext != null)
                {
                    return new VectorChatMessageStore(
                        _chatHistoryDbContext,
                        ctx.SerializedState,
                        ctx.JsonSerializerOptions,
                        logger);
                }

                throw new InvalidOperationException("ChatHistoryDbContext was not provided. Register ChatHistoryDbContext in DI to enable MSSQL chat history storage.");
            }
        };

        return CreateAgentUsingReflection(_chatClientObj, orchestratorOptions);
    }

    public AIAgent CreateAgent(ChatClientAgentOptions options)
    {
        if (_chatClientObj == null)
        {
            throw new InvalidOperationException("OpenAI Chat client is not initialized. Set `AgentConfiguration:OpenAIKey` or the `OPENAI_API_KEY` environment variable to enable agent creation.");
        }

        return CreateAgentUsingReflection(_chatClientObj, options);
    }

    public AIAgent CreateAgent(ChatClientAgentOptions options, string? deploymentName, string? endpoint)
    {
        // For the OpenAI (non-Azure) path, ignore deploymentName/endpoint override
        // and create the agent using the configured API key. If you need to target
        // a different OpenAI base URL or model, update `AgentConfiguration` and
        // supply a different `OpenAIKey` or extend this method accordingly.
        if (_chatClientObj == null)
        {
            throw new InvalidOperationException("OpenAI Chat client is not initialized. Set `AgentConfiguration:OpenAIKey` or the `OPENAI_API_KEY` environment variable to enable agent creation.");
        }

        return CreateAgentUsingReflection(_chatClientObj, options);
    }

    private ChatClientAgent CreateAgentUsingReflection(OpenAIClient? chatClientObj, ChatClientAgentOptions options)
    {
        if (chatClientObj == null)
            throw new InvalidOperationException("Chat client instance is null.");

        return chatClientObj
            .GetChatClient("gpt-4o-mini")
            .CreateAIAgent(options);

    }
}

