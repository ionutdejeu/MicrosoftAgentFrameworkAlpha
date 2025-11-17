using AgentWebApi.Models;
using Microsoft.Agents.AI;

namespace AgentWebApi.Interfaces;

public interface IOrchestratorAgentService
{
    Task<AgentResponse> AskOrchestratorAsync(string question, string? threadId = null);
}

