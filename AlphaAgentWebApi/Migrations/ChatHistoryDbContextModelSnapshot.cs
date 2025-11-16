using System;
using AlphaAgentWebApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AlphaAgentWebApi.Migrations
{
    [DbContext(typeof(ChatHistoryDbContext))]
    partial class ChatHistoryDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.0");

            modelBuilder.Entity("AlphaAgentWebApi.Stores.Entities.ChatThread", b =>
            {
                b.Property<string>("ThreadId").HasMaxLength(100);
                b.Property<string>("Title").HasMaxLength(500);
                b.Property<DateTimeOffset>("CreatedAt");
                b.Property<DateTimeOffset>("LastUpdatedAt");
                b.HasKey("ThreadId");
                b.ToTable("ChatThreads");
            });

            modelBuilder.Entity("AlphaAgentWebApi.Stores.Entities.ChatMessageEntity", b =>
            {
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.Property<string>("MessageId").HasMaxLength(200);
                b.Property<string>("ThreadId").IsRequired().HasMaxLength(100);
                b.Property<string>("Role").HasMaxLength(50);
                b.Property<string>("ContentType").HasMaxLength(50);
                b.Property<string>("TextContent");
                b.Property<string>("ImageUrl").HasMaxLength(1000);
                b.Property<string>("FunctionCallName").HasMaxLength(200);
                b.Property<string>("FunctionCallArgumentsJson");
                b.Property<string>("ToolName").HasMaxLength(200);
                b.Property<string>("ToolResponseJson");
                b.Property<string>("RawContentJson");
                b.Property<string>("SerializedMessage");
                b.Property<DateTimeOffset>("CreatedAt");
                b.HasKey("Id");
                b.HasIndex("ThreadId");
                b.ToTable("ChatMessages");
            });
        }
    }
}
