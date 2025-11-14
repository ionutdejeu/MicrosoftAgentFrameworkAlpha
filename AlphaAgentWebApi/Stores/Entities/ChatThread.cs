using System;
using System.Collections.Generic;

namespace AlphaAgentWebApi.Stores.Entities;

public sealed class ChatThread
{
    // Primary key: stored as string to keep compatibility with existing ThreadDbKey values
    public string ThreadId { get; set; } = Guid.NewGuid().ToString("N");

    // Optional title or descriptor
    public string? Title { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation property
    public List<ChatMessageEntity> Messages { get; set; } = new();
}
