using AgentWebApi.Models;

namespace AgentWebApi.Interfaces;

public interface IGeographyAgentService
{
    Task<GeographyResponse> AskGeographyAsync(string question);
}

