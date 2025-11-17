using Microsoft.AspNetCore.Mvc;
using AgentWebApi.Interfaces;
using AgentWebApi.Models;

namespace AgentWebApi.Controllers;
 
[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly IGeographyAgentService _geographyAgentService;
    private readonly IMathAgentService _mathAgentService;
    private readonly IOrchestratorAgentService _orchestratorAgentService;

    public AgentController(IGeographyAgentService geographyAgentService, IMathAgentService mathAgentService, IOrchestratorAgentService orchestratorAgentService)
    {
        _geographyAgentService = geographyAgentService;
        _mathAgentService = mathAgentService;
        _orchestratorAgentService = orchestratorAgentService;
    }

    [HttpPost("geography")]
    public async Task<ActionResult<GeographyResponse>> AskGeography([FromBody] AgentRequest request)
    {
        try
        {
            var geographyResponse = await _geographyAgentService.AskGeographyAsync(request.Question);
            return Ok(geographyResponse);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("math")]
    public async Task<ActionResult<AgentResponse>> AskMath([FromBody] AgentRequest request)
    {
        var response = await _mathAgentService.AskMathAsync(request.Question);
        return Ok(response);
    }

    [HttpPost("orchestrator")]
    public async Task<ActionResult<AgentResponse>> AskOrchestrator([FromBody] AgentRequest request)
    {
        var response = await _orchestratorAgentService.AskOrchestratorAsync(request.Question, request.ThreadId);
        return Ok(response);
    }
}
