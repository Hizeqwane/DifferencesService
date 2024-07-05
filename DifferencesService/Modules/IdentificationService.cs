using System.Reflection;
using DifferencesService.Interfaces;
using DifferencesService.Options;

namespace DifferencesService.Modules;

public class IdentificationService : IIdentificationService
{
    private readonly string _defaultIdentificationPropertyName;
    private readonly Dictionary<Type, string> _identificationIdPropertyNameMap = new();
    private readonly Dictionary<Type, IIdentificationProvider> _identificationProvidersMap = new();

    public IdentificationService(DifferenceServiceOptions options)
    {
        _identificationProvidersMap = options.IdentificationProvidersMap;
        _identificationIdPropertyNameMap = options.IdentificationIdPropertyNameMap;
        _defaultIdentificationPropertyName = options.DefaultIdPropertyName;
    }
    
    public PropertyInfo FindIdPropertyAndThrow(
        Type typeOfObject,
        PropertyInfo[]? properties = null)
    {
        properties ??= typeOfObject.GetProperties();
        var idProperty = properties.FirstOrDefault(s => s.Name == GetIdPropertyName(typeOfObject));
        if (idProperty == null)
            throw new InvalidDataException($"Идентификационное свойство {GetIdPropertyName(typeOfObject)} не найдено для объекта типа {typeOfObject.FullName}.");
        
        return idProperty;
    }
    
    public TId GetNextId<TId>()
    {
        if (!_identificationProvidersMap.TryGetValue(typeof(TId), out var foundedProvider))
            throw new ArgumentException($"Для типа {typeof(TId)} не зарегистрирован {nameof(IIdentificationProvider)}.");

        return (TId)foundedProvider.GetNextObjectId();
    }

    public void Flush<TId>()
    {
        if (!_identificationProvidersMap.TryGetValue(typeof(TId), out var foundedProvider))
            throw new ArgumentException($"Для типа {typeof(TId)} не зарегистрирован {nameof(IIdentificationProvider)}.");

        foundedProvider.Flush();
    }

    public string GetIdPropertyName(Type objType) =>
        _identificationIdPropertyNameMap.FirstOrDefault(s => s.Key == objType).Value ??
        _defaultIdentificationPropertyName;
}