using Microsoft.Extensions.DependencyInjection;
using TeamForge.Agents;

namespace TeamForge.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTeamForgeApplication(this IServiceCollection services)
    {
        services.AddScoped<IAgent<BusinessAnalysisInput, BusinessAnalysisResult>, BusinessAnalystAgent>();
        services.AddScoped<IAgent<DeveloperAnalysisInput, DeveloperAnalysisResult>, DeveloperAgent>();
        services.AddScoped<IAgent<QaAnalysisInput, QaAnalysisResult>, QaAgent>();
        services.AddScoped<RequirementWorkflow>();
        return services;
    }
}
