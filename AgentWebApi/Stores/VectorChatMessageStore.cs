using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgentWebApi.Entities;
using Microsoft.EntityFrameworkCore;
using AgentWebApi.Data;

namespace AgentWebApi.Stores;
internal sealed class VectorChatMessageStore : ChatMessageStore
{
    private readonly ChatHistoryDbContext _dbContext;
    private readonly ILogger<VectorChatMessageStore>? _logger;
    private readonly JsonSerializerOptions? _jsonOptions;

    public VectorChatMessageStore(
        ChatHistoryDbContext dbContext,
        JsonElement serializedStoreState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        ILogger<VectorChatMessageStore>? logger = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger;
        _jsonOptions = jsonSerializerOptions;

        if (serializedStoreState.ValueKind is JsonValueKind.String)
        {
            var threadIdString = serializedStoreState.GetString();
            if (!string.IsNullOrWhiteSpace(threadIdString))
            {
                this.ThreadDbKey = threadIdString;
            }
        }
    }

    public string? ThreadDbKey { get; internal set; }

    public override async Task AddMessagesAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken)
    {
        this.ThreadDbKey ??= Guid.NewGuid().ToString("N");

        var messagesList = messages.ToList();

        // Ensure thread exists
        var thread = await _dbContext.ChatThreads.FindAsync(new object[] { this.ThreadDbKey }, cancellationToken);
        if (thread == null)
        {
            thread = new ChatThread
            {
                ThreadId = this.ThreadDbKey,
                CreatedAt = DateTimeOffset.UtcNow,
                LastUpdatedAt = DateTimeOffset.UtcNow
            };
            await _dbContext.ChatThreads.AddAsync(thread, cancellationToken);
        }

        foreach (var msg in messagesList)
        {
            // Serialize original message for fidelity
            var serialized = JsonSerializer.Serialize(msg, _jsonOptions);

            var entity = new ChatMessageEntity
            {
                MessageId = string.IsNullOrWhiteSpace(msg.MessageId) ? null : msg.MessageId,
                ThreadId = this.ThreadDbKey,
                Role = msg.Role.ToString(),
                SerializedMessage = serialized,
                TextContent = msg.Text,
                CreatedAt = DateTimeOffset.UtcNow
            };

            // Try to detect function_call or image in the serialized payload and populate specific columns
            try
            {
                using var doc = JsonDocument.Parse(serialized);
                var root = doc.RootElement;

                if (root.TryGetProperty("function_call", out var funcProp) || root.TryGetProperty("functionCall", out funcProp))
                {
                    entity.ContentType = "function_call";
                    if (funcProp.ValueKind == JsonValueKind.Object)
                    {
                        if (funcProp.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                        {
                            entity.FunctionCallName = nameProp.GetString();
                        }

                        if (funcProp.TryGetProperty("arguments", out var argsProp))
                        {
                            // store arguments as JSON string
                            entity.FunctionCallArgumentsJson = argsProp.GetRawText();
                        }
                    }
                }
                else if (root.TryGetProperty("image", out var imageProp) || root.TryGetProperty("images", out imageProp))
                {
                    entity.ContentType = "image";
                    // try to find a url inside
                    if (imageProp.ValueKind == JsonValueKind.Object && imageProp.TryGetProperty("url", out var url))
                    {
                        entity.ImageUrl = url.GetString();
                    }
                }
                else if (!string.IsNullOrWhiteSpace(msg.Text))
                {
                    entity.ContentType = "text";
                }
                else
                {
                    entity.ContentType = "other";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed to inspect serialized message for content-type detection");
                entity.ContentType = "unknown";
            }

            await _dbContext.ChatMessages.AddAsync(entity, cancellationToken);
        }

        thread.LastUpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public override async Task<IEnumerable<ChatMessage>> GetMessagesAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(this.ThreadDbKey))
        {
            return Array.Empty<ChatMessage>();
        }

        var records = _dbContext.ChatMessages
            .Where(m => m.ThreadId == this.ThreadDbKey)
            .OrderBy(m => m.Id);

        var list = new List<ChatMessage>();
        await foreach (var rec in records.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(rec.SerializedMessage))
            {
                try
                {
                    var msg = JsonSerializer.Deserialize<ChatMessage>(rec.SerializedMessage, _jsonOptions);
                    if (msg != null)
                    {
                        list.Add(msg);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to deserialize stored ChatMessage, falling back to TextContent");
                }
            }

            // Fallback: construct a basic ChatMessage using text if available
            var fallbackJson = rec.SerializedMessage ?? JsonSerializer.Serialize(new { text = rec.TextContent });
            try
            {
                var fallback = JsonSerializer.Deserialize<ChatMessage>(fallbackJson, _jsonOptions);
                if (fallback != null)
                {
                    list.Add(fallback);
                }
            }
            catch
            {
                // last fallback: ignore
            }
        }

        return list;
    }

    public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null)
    {
        if (string.IsNullOrWhiteSpace(this.ThreadDbKey))
        {
            this.ThreadDbKey = Guid.NewGuid().ToString("N");
        }
        return JsonSerializer.SerializeToElement(this.ThreadDbKey);
    }
}