using Microsoft.Agents.AI;

namespace AgentWebApi.Interfaces;

public interface IAgentProvider
{
    AIAgent GetGeographyAgent();
    AIAgent GetMathAgent();
    AIAgent GetOrchestratorAgent();
    AIAgent GetAgent(string agentName);
    AIAgent CreateAgent(ChatClientAgentOptions options);
    AIAgent CreateAgent(ChatClientAgentOptions options, string? deploymentName, string? endpoint);
}

