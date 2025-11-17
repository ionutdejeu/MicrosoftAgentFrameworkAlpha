using Microsoft.EntityFrameworkCore;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
// Add Swagger/OpenAPI services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Register controllers so controller routes are discovered
builder.Services.AddControllers();
// Register DbContext for ChatHistory using SQL Server
builder.Services.AddDbContext<AgentWebApi.Data.ChatHistoryDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ChatHistorySql")));
// Bind AgentConfiguration from appsettings
builder.Services.Configure<AgentWebApi.Configuration.AgentConfiguration>(builder.Configuration.GetSection("AgentConfiguration"));

// Configure CORS using the `AllowedHosts` value from configuration.
var allowedHostsConfig = builder.Configuration["AllowedHosts"] ?? "*";
const string corsPolicyName = "AllowedHostsPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicyName, policyBuilder =>
    {
        if (allowedHostsConfig.Trim() == "*")
        {
            policyBuilder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            var origins = allowedHostsConfig
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToArray();
            policyBuilder.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

// Register core services and implementations
// AgentProvider depends on scoped DbContext for chat history; register as scoped
builder.Services.AddScoped<AgentWebApi.Interfaces.IAgentProvider, AgentWebApi.Services.AgentProvider>();
builder.Services.AddScoped<AgentWebApi.Interfaces.IGeographyAgentService, AgentWebApi.Services.GeographyAgentService>();
builder.Services.AddScoped<AgentWebApi.Interfaces.IMathAgentService, AgentWebApi.Services.MathAgentService>();
builder.Services.AddScoped<AgentWebApi.Interfaces.IOrchestratorAgentService, AgentWebApi.Services.OrchestratorAgentService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
// Enable middleware to serve generated Swagger as JSON endpoint.
app.UseSwagger();

// Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.)
// Swagger page will be available at `/swagger`.
app.UseSwaggerUI();

app.UseHttpsRedirection();

// Apply CORS policy configured from `AllowedHosts` before authorization
app.UseCors("AllowedHostsPolicy");

app.UseAuthorization();

// Map attribute routed controllers from the Controllers folder
app.MapControllers();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
