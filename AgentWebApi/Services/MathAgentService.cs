using AgentWebApi.Interfaces;
using AgentWebApi.Models;
using Microsoft.Agents.AI;

namespace AgentWebApi.Services;

public class MathAgentService : IMathAgentService
{
    private readonly AIAgent _mathAgent;

    public MathAgentService(IAgentProvider agentProvider)
    {
        _mathAgent = agentProvider.GetMathAgent();
    }

    public async Task<AgentResponse> AskMathAsync(string question)
    {
        var result = await _mathAgent.RunAsync(question);
        return new AgentResponse
        {
            Response = result.ToString()
        };
    }
}

