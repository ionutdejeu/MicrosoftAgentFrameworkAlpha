using AgentWebApi.Interfaces;
using AgentWebApi.Models;
using Microsoft.Agents.AI;
using System.Text.Json;

namespace AgentWebApi.Services;

public class GeographyAgentService : IGeographyAgentService
{
    private readonly AIAgent _geographyAgent;

    public GeographyAgentService(IAgentProvider agentProvider)
    {
        _geographyAgent = agentProvider.GetGeographyAgent();
    }

    public async Task<GeographyResponse> AskGeographyAsync(string question)
    {
        var result = await _geographyAgent.RunAsync(question);
        var jsonContent = result.ToString();
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        try
        {
            var geographyResponse = JsonSerializer.Deserialize<GeographyResponse>(jsonContent, jsonOptions);
            if (geographyResponse == null)
            {
                throw new InvalidOperationException($"Empty geography response received. Raw response: {jsonContent}");
            }
            return geographyResponse;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to deserialize geography response. Raw response: {jsonContent}", ex);
        }
    }
}

