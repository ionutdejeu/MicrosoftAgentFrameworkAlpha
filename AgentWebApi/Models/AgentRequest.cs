namespace AgentWebApi.Models;

public class AgentRequest
{
    public string Question { get; set; } = string.Empty;
    public string? ThreadId { get; set; }
}
