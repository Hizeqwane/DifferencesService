using System.Reflection;
using DifferencesService.Interfaces;
using DifferencesService.Models;
using DifferencesService.Options;

namespace DifferencesService.Modules;

public class IdentificationService : IIdentificationService
{
    private readonly string _defaultIdentificationPropertyName;
    private readonly Dictionary<Type, string> _identificationIdPropertyNameMap = new();
    private readonly IIdentificationProvider _identificationProvider;

    public IdentificationService(DifferenceServiceOptions options)
    {
        _identificationProvider = options.IdentificationProvider ?? DifferenceServiceOptions.DefaultIdentificationProvider;
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
            throw new IdPropertyNotFoundException($"Идентификационное свойство {GetIdPropertyName(typeOfObject)} не найдено для объекта типа {typeOfObject.FullName}.");
        
        return idProperty;
    }
    
    public object GetNextId() => _identificationProvider.GetNextObjectId();

    public void Flush() => _identificationProvider.Flush();

    public string GetIdPropertyName(Type objType) =>
        _identificationIdPropertyNameMap.FirstOrDefault(s => s.Key == objType).Value ??
        _defaultIdentificationPropertyName;
}