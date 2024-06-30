using DifferencesService.Interfaces;
using DifferencesService.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace DifferencesService;

public static class ServiceCollectionExtensions
{
    public static void UseDifferenceService<TId>(this IServiceCollection services)
    {
        services.AddScoped<IIdentificatorProvider<TId>, IdentificatorProvider<TId>>();
        services.AddScoped<IIdentificatorProvider<TId>, IdentificatorProvider<TId>>();
    }
}