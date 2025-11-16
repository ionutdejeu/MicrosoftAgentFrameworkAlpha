using AlphaAgentWebApi.Interfaces;
using AlphaAgentWebApi.Configuration;
using AlphaAgentWebApi.Constants;
using AlphaAgentWebApi.Models;
using AlphaAgentWebApi.Stores;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Connectors.MongoDB;
using MongoDB.Driver;
using OpenAI;
using AlphaAgentWebApi.Data;

namespace AlphaAgentWebApi.Services;

public class AgentProvider : IAgentProvider
{
    private readonly AIAgent _geographyAgent;
    private readonly AIAgent _mathAgent;
    private readonly AIAgent _orchestratorAgent;
    private readonly OpenAI.Chat.ChatClient _chatClient;
    private readonly AgentConfiguration _config;
    private readonly string _deploymentName;
    private readonly string _endpoint;
    private readonly DefaultAzureCredential _credential;
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

        // Initialize Azure OpenAI connection (previously in AgentFactory)
        _deploymentName =
            Environment.GetEnvironmentVariable(_config.PsDeploymentEnvName)
            ?? throw new ArgumentNullException(nameof(_config.PsDeploymentEnvName), $"{_config.PsDeploymentEnvName} environment variable is not set");

        _endpoint =
            Environment.GetEnvironmentVariable(_config.PsEndpointEnvName)
            ?? throw new ArgumentNullException(nameof(_config.PsEndpointEnvName), $"{_config.PsEndpointEnvName} environment variable is not set");

        var authOptions = new DefaultAzureCredentialOptions { ExcludeAzureDeveloperCliCredential = false };
        _credential = new DefaultAzureCredential(authOptions);

        _chatClient = new AzureOpenAIClient(new Uri(_endpoint), _credential)
            .GetChatClient(_deploymentName);

        // Create agents once during initialization (singleton pattern)
        _geographyAgent = CreateGeographyAgent();
        _mathAgent = CreateMathAgent();
        _orchestratorAgent = CreateOrchestratorAgent();
    }

    public AIAgent GetGeographyAgent() => _geographyAgent;

    public AIAgent GetMathAgent() => _mathAgent;

    public AIAgent GetOrchestratorAgent() => _orchestratorAgent;

    public AIAgent GetAgent(string agentName)
    {
        return agentName switch
        {
            AgentNames.GeographyAgent => _geographyAgent,
            AgentNames.MathAgent => _mathAgent,
            AgentNames.OrchestratorAgent => _orchestratorAgent,
            _ => throw new InvalidOperationException($"{agentName} agent not found or not supported")
        };
    }

    private AIAgent CreateGeographyAgent()
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

        return _chatClient.CreateAIAgent(geographyOptions);
    }

    private AIAgent CreateMathAgent()
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

        return _chatClient.CreateAIAgent(mathOptions);
    }

    private AIAgent CreateOrchestratorAgent()
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

        return _chatClient.CreateAIAgent(orchestratorOptions);
    }

    public AIAgent CreateAgent(ChatClientAgentOptions options)
    {
        return _chatClient.CreateAIAgent(options);
    }

    public AIAgent CreateAgent(ChatClientAgentOptions options, string? deploymentName, string? endpoint)
    {
        bool hasDeployment = !string.IsNullOrWhiteSpace(deploymentName);
        bool hasEndpoint = !string.IsNullOrWhiteSpace(endpoint);

        if (hasDeployment ^ hasEndpoint)
        {
            throw new ArgumentException("Both deploymentName and endpoint must be provided together when overriding.");
        }

        string effectiveDeployment = hasDeployment ? deploymentName! : _deploymentName;
        string effectiveEndpoint = hasEndpoint ? endpoint! : _endpoint;

        ArgumentException.ThrowIfNullOrWhiteSpace(effectiveDeployment);
        ArgumentException.ThrowIfNullOrWhiteSpace(effectiveEndpoint);

        return new AzureOpenAIClient(new Uri(effectiveEndpoint), _credential)
            .GetChatClient(effectiveDeployment)
            .CreateAIAgent(options);
    }
}

