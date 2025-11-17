using AgentWebApi.Interfaces;
using AgentWebApi.Configuration;
using AgentWebApi.Constants;
using AgentWebApi.Models;
using AgentWebApi.Stores;
using OpenAI;
using Microsoft.Agents.AI;
using System.Reflection;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
// Removed SemanticKernel.MongoDB and MongoDB.Driver references: not used in this project
using AgentWebApi.Data;

namespace AgentWebApi.Services;

public class AgentProvider : IAgentProvider
{
    private readonly AIAgent? _geographyAgent;
    private readonly AIAgent? _mathAgent;
    private readonly AIAgent? _orchestratorAgent;
    private readonly object? _chatClientObj;
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

        if (string.IsNullOrWhiteSpace(_openAiKey))
        {
            // Missing OpenAI API key - do not initialize clients/agents.
            // This allows the application to start for development scenarios where secrets are not set.
            _chatClientObj = null;
            _geographyAgent = null;
            _mathAgent = null;
            _orchestratorAgent = null;
            return;
        }

        // Construct the OpenAI Chat client using reflection so this code
        // is tolerant to multiple OpenAI SDK versions.
        object? chatClientInstance = null;
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var chatClientType = assemblies
            .SelectMany(a =>
            {
                try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
            })
            .FirstOrDefault(t => t.FullName == "OpenAI.Chat.ChatClient");

        if (chatClientType != null)
        {
            // Try static factory methods that accept a single string (API key)
            var staticFactory = chatClientType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));

            if (staticFactory != null)
            {
                chatClientInstance = staticFactory.Invoke(null, new object[] { _openAiKey! });
            }
            else
            {
                // Try a public constructor that accepts string
                var ctorWithString = chatClientType.GetConstructor(new[] { typeof(string) });
                if (ctorWithString != null)
                {
                    chatClientInstance = ctorWithString.Invoke(new object[] { _openAiKey! });
                }
                else
                {
                    // As a last resort try to create non-public instance
                    try
                    {
                        chatClientInstance = Activator.CreateInstance(chatClientType, nonPublic: true);
                        // If there is a writable ApiKey property, set it
                        var apiKeyProp = chatClientType.GetProperty("ApiKey", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                        if (apiKeyProp != null && apiKeyProp.CanWrite)
                        {
                            apiKeyProp.SetValue(chatClientInstance, _openAiKey);
                        }
                    }
                    catch
                    {
                        chatClientInstance = null;
                    }
                }
            }
        }

        _chatClientObj = chatClientInstance;

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

        return CreateAgentUsingReflection(_chatClientObj, geographyOptions);
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

        return CreateAgentUsingReflection(_chatClientObj, mathOptions);
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

    private AIAgent CreateAgentUsingReflection(object? chatClientObj, ChatClientAgentOptions options)
    {
        if (chatClientObj == null)
            throw new InvalidOperationException("Chat client instance is null.");

        // Find the extension type that defines CreateAIAgent
        var extType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
            })
            .FirstOrDefault(t => t.Name == "OpenAIChatClientExtensions" || t.FullName == "Microsoft.Agents.AI.OpenAI.OpenAIChatClientExtensions");

        if (extType == null)
            throw new InvalidOperationException("Could not locate OpenAIChatClientExtensions in loaded assemblies.");

        // Find a suitable CreateAIAgent overload
        var methods = extType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == "CreateAIAgent").ToArray();

        MethodInfo? createMethod = null;
        foreach (var m in methods)
        {
            var ps = m.GetParameters();
            if (ps.Length >= 2 && ps[0].ParameterType.FullName == chatClientObj.GetType().FullName && ps[1].ParameterType == typeof(ChatClientAgentOptions))
            {
                createMethod = m;
                break;
            }
        }

        // If not found by exact match, pick first overload with first parameter assignable from the chat client type
        if (createMethod == null)
        {
            foreach (var m in methods)
            {
                var ps = m.GetParameters();
                if (ps.Length >= 2 && ps[1].ParameterType == typeof(ChatClientAgentOptions) && ps[0].ParameterType.IsAssignableFrom(chatClientObj.GetType()))
                {
                    createMethod = m;
                    break;
                }
            }
        }

        if (createMethod == null)
            throw new InvalidOperationException("Suitable CreateAIAgent method not found on OpenAIChatClientExtensions.");

        // Prepare arguments: (chatClient, options, clientFactory?, loggerFactory?, services?)
        var psCount = createMethod.GetParameters().Length;
        var args = new List<object?> { chatClientObj, options };
        // Add optional parameters as null or logger factory
        if (psCount >= 3) args.Add(null);
        if (psCount >= 4) args.Add(_loggerFactory);
        if (psCount >= 5) args.Add(null);

        var result = createMethod.Invoke(null, args.ToArray());
        if (result is AIAgent agent)
            return agent;

        throw new InvalidOperationException("CreateAIAgent did not return an AIAgent instance.");
    }
}

