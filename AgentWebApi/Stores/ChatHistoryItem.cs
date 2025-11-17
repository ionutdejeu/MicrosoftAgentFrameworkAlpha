namespace AgentWebApi.Stores;

internal sealed class ChatHistoryItem
{
    public string? Key { get; set; }
    public string? ThreadId { get; set; }
    public long Timestamp { get; set; }
    public string? SerializedMessage { get; set; }
    public string? MessageText { get; set; }
}

