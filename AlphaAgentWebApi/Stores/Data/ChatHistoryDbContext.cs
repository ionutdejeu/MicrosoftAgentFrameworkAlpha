using System;
using Microsoft.EntityFrameworkCore;
using AlphaAgentWebApi.Stores.Entities;

namespace AlphaAgentWebApi.Stores.Data;

public class ChatHistoryDbContext : DbContext
{
    public ChatHistoryDbContext(DbContextOptions<ChatHistoryDbContext> options) : base(options)
    {
    }

    public DbSet<ChatThread> ChatThreads { get; set; } = null!;
    public DbSet<ChatMessageEntity> ChatMessages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ChatThread>(b =>
        {
            b.HasKey(t => t.ThreadId);
            b.Property(t => t.ThreadId).HasMaxLength(100);
            b.Property(t => t.Title).HasMaxLength(500);
            b.Property(t => t.CreatedAt).IsRequired();
            b.Property(t => t.LastUpdatedAt).IsRequired();
        });

        modelBuilder.Entity<ChatMessageEntity>(b =>
        {
            b.HasKey(m => m.Id);
            b.Property(m => m.ThreadId).HasMaxLength(100).IsRequired();
            b.HasIndex(m => m.ThreadId);
            b.Property(m => m.Role).HasMaxLength(50);
            b.Property(m => m.ContentType).HasMaxLength(50);
            b.Property(m => m.ImageUrl).HasMaxLength(1000);
            b.Property(m => m.FunctionCallName).HasMaxLength(200);
            b.Property(m => m.ToolName).HasMaxLength(200);
            b.Property(m => m.MessageId).HasMaxLength(200);

            b.HasOne<ChatThread>()
                .WithMany(t => t.Messages)
                .HasForeignKey(m => m.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
