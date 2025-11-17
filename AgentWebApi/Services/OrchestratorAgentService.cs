using AgentWebApi.Interfaces;
using AgentWebApi.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AgentWebApi.Services;

public class OrchestratorAgentService : IOrchestratorAgentService
{
    private readonly AIAgent _orchestratorAgent;
    private readonly AIAgent _geographyAgent;
    private readonly AIAgent _mathAgent;
    private readonly ILogger<OrchestratorAgentService> _logger;

    public OrchestratorAgentService(IAgentProvider agentProvider, ILogger<OrchestratorAgentService> logger)
    {
        _orchestratorAgent = agentProvider.GetOrchestratorAgent();
        _geographyAgent = agentProvider.GetGeographyAgent();
        _mathAgent = agentProvider.GetMathAgent();
        _logger = logger;
    }

    public async Task<AgentResponse> AskOrchestratorAsync(string question, string? threadId = null)
    {
        AgentThread thread;
        
        if (string.IsNullOrWhiteSpace(threadId))
        {
            thread = _orchestratorAgent.GetNewThread();
        }
        else
        {
            var agentThreadState = new AgentThreadState { StoreState = threadId };
            var threadStateElement = JsonSerializer.SerializeToElement(agentThreadState);
            thread = _orchestratorAgent.DeserializeThread(threadStateElement);
        }

        var result = await _orchestratorAgent.RunAsync(question, thread);
        var serializedState = thread.Serialize();
        var extractedThreadId = ExtractThreadIdFromState(serializedState);
        
        if (string.IsNullOrWhiteSpace(extractedThreadId) && !string.IsNullOrWhiteSpace(threadId))
        {
            extractedThreadId = threadId;
        }

        return new AgentResponse
        {
            Response = result.ToString(),
            ThreadId = extractedThreadId
        };
    }

    private string? ExtractThreadIdFromState(JsonElement serializedState)
    {
        if (serializedState.ValueKind == JsonValueKind.Object)
        {
            if (serializedState.TryGetProperty("storeState", out var storeStateElement))
            {
                if (storeStateElement.ValueKind == JsonValueKind.String)
                {
                    var threadId = storeStateElement.GetString();
                    if (!string.IsNullOrWhiteSpace(threadId))
                    {
                        return threadId;
                    }
                }
            }

            try
            {
                var agentThreadState = JsonSerializer.Deserialize<AgentThreadState>(serializedState);
                if (!string.IsNullOrWhiteSpace(agentThreadState?.StoreState))
                {
                    return agentThreadState.StoreState;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize thread state as AgentThreadState. Manual extraction may have already succeeded.");
            }
        }
        
        if (serializedState.ValueKind == JsonValueKind.String)
        {
            return serializedState.GetString();
        }

        return null;
    }
}


