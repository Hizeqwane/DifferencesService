using DifferencesService.Interfaces;

namespace DifferencesService.Options;

public static class DifferenceServiceExt
{
    public static DifferenceServiceOptions SetupIdentificationProvider(this DifferenceServiceOptions options,
        IIdentificationProvider provider)
    {
        options.SetIdentificationProvider(provider);
        
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