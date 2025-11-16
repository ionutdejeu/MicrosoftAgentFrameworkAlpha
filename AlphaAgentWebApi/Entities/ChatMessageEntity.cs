using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlphaAgentWebApi.Entities;

// This entity stores one chat message. It contains dedicated columns for the common OpenAI message
// content types: text, image, function_call (name + arguments), tool responses and a RawJson fallback.
public sealed class ChatMessageEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    // Optional message id coming from the ChatMessage object
    [MaxLength(200)]
    public string? MessageId { get; set; }

    // FK to ChatThread.ThreadId
    [Required]
    [MaxLength(100)]
    public string ThreadId { get; set; } = string.Empty;

    // Role: user, assistant, system, etc.
    [MaxLength(50)]
    public string? Role { get; set; }

    // A simple discriminator describing the content type (e.g., "text", "image", "function_call", "tool_response", "other")
    [MaxLength(50)]
    public string? ContentType { get; set; }

    // Text content (plain text). Used for typical chat responses and prompts.
    public string? TextContent { get; set; }

    // Image URL or reference when the content is an image
    [MaxLength(1000)]
    public string? ImageUrl { get; set; }

    // Function call name and arguments if the message is a function call
    [MaxLength(200)]
    public string? FunctionCallName { get; set; }

    // JSON arguments for the function call (stringified)
    public string? FunctionCallArgumentsJson { get; set; }

    // Tool name (if applicable) and JSON response
    [MaxLength(200)]
    public string? ToolName { get; set; }
    public string? ToolResponseJson { get; set; }

    // Any additional/raw content as JSON (fallback for unknown content shapes)
    public string? RawContentJson { get; set; }

    // Original serialized ChatMessage for full fidelity
    public string? SerializedMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
