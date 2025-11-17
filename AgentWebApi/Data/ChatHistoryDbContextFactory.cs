using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AgentWebApi.Data;

public class ChatHistoryDbContextFactory : IDesignTimeDbContextFactory<ChatHistoryDbContext>
{
    public ChatHistoryDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("ChatHistorySql");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'ChatHistorySql' was not found. Please set it in appsettings.json or environment variables.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ChatHistoryDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new ChatHistoryDbContext(optionsBuilder.Options);
    }
}
