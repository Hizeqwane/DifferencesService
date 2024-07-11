using DifferencesService.Interfaces;
using DifferencesService.Modules;
using DifferencesService.Options;
using Microsoft.Extensions.DependencyInjection;

namespace DifferencesService;

public static class ServiceExtensions
{
    /// <summary>
    /// По умолчанию будет добавлен IntIdentificationProvider
    /// </summary>
    /// <param name="services"></param>
    /// <typeparam name="TId"></typeparam>
    /// <returns></returns>
    public static IServiceCollection UseDifferenceService<TId>(this IServiceCollection services) => 
        services.UseDifferenceService<TId>(options => options);
    
    /// <summary>
    /// По умолчанию будет добавлен IntIdentificationProvider
    /// </summary>
    /// <param name="services"></param>
    /// <param name="setup"></param>
    /// <typeparam name="TId"></typeparam>
    /// <returns></returns>
    public static IServiceCollection UseDifferenceService<TId>(this IServiceCollection services, Func<DifferenceServiceOptions, DifferenceServiceOptions> setup)
    {
        var options = setup(new DifferenceServiceOptions());
        
        if (options.IdentificationProvider == null) 
            options.SetDefaultIdentificationProvider();
        
        services.AddScoped<DifferenceServiceOptions>(_ => options);
        services.AddScoped<IDifferenceHandler, DifferencesHandler>();
        services.AddScoped<IIdentificationService, IdentificationService>();
        services.AddScoped<IDifferenceObjectProvider, DifferenceObjectProvider>();

        return services;
    }
}