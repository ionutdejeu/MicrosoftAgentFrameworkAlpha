using AgentWebApi.Models;

namespace AgentWebApi.Interfaces;

public interface IMathAgentService
{
    Task<AgentResponse> AskMathAsync(string question);
}

