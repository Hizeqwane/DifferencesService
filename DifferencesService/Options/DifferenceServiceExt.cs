using DifferencesService.Interfaces;

namespace DifferencesService.Options;

public static class DifferenceServiceExt
{
    public static DifferenceServiceOptions SetupIdentificationProvider(this DifferenceServiceOptions options,
        IIdentificationProvider provider)
    {
        if (!options.IdentificationProvidersMap.TryAdd(provider.GetIdType(), provider))
            throw new ArgumentException($"Для типа {provider.GetType()} зарегистрировано более одного {nameof(IIdentificationProvider)}.");
        
        return options;
    }

    public static DifferenceServiceOptions SetupIdPropertyNameForType<T>(this DifferenceServiceOptions options,
        string idPropertyName)
    {
        if (!options.IdentificationIdPropertyNameMap.TryAdd(typeof(T), idPropertyName))
            throw new ArgumentException($"Для типа {typeof(T)} зарегистрировано более одного {nameof(idPropertyName)}.");
        
        return options;
    }

    public static DifferenceServiceOptions WithDefaultIdPropertyName(this DifferenceServiceOptions options,
        string defaultIdPropertyName)
    {
        options.DefaultIdPropertyName = defaultIdPropertyName;
        
        return options;
    }
}