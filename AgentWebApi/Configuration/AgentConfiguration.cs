namespace AgentWebApi.Configuration;

public class AgentConfiguration
{
    public string PsDeploymentEnvName { get; set; } = "PS_AZURE_AI_FOUNDRY_OPEN_AI_DEPLOYMENT_NAME";
    public string PsEndpointEnvName { get; set; } = "PS_AZURE_AI_FOUNDRY_OPEN_AI_ENDPOINT";
    public string OpenAiDeploymentEnvName { get; set; } = "OPEN_AI_DEPLOYMENT_NAME";
    public string OpenAiEndpointEnvName { get; set; } = "OPEN_AI_AZURE_OPEN_AI_ENDPOINT";
    public string OpenAIKey { get; set; } = string.Empty;
    public string MongoDbConnectionString { get; set; } = string.Empty;
    public string MongoDbDatabaseName { get; set; } = string.Empty;
    public Dictionary<string, AgentSettings> Agents { get; set; } = new();
}

public class AgentSettings
{
    public string Name { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
}
