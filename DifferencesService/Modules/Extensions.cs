using System.ComponentModel;
using System.Reflection;

namespace DifferencesService.Modules;

public static class Extensions
{
    public static string IdentificationPropertyName { get; private set; } = "Id";

    public static void SetIdentificationPropertyName(string identificationPropertyName) => 
        IdentificationPropertyName = identificationPropertyName;

    public static PropertyInfo FindIdPropertyAndThrow(
        this Type typeOfObject,
        PropertyInfo[]? properties = null)
    {
        properties ??= typeOfObject.GetProperties();
        var idProperty = properties.FirstOrDefault(s => s.Name == IdentificationPropertyName);
        if (idProperty == null)
            throw new InvalidDataException($"Идентификационное свойство {IdentificationPropertyName} не найдено для объекта типа {typeOfObject.FullName}.");
        
        return idProperty;
    }

    public static bool IsEqualsFromToString(this object? primaryObjId, object? secondaryObjId)
    {
        if (primaryObjId == null)
            throw new ArgumentException("Идентификационно свойство для сравнения не может быть равно null.");
        
        return primaryObjId.ToString() == secondaryObjId?.ToString();
    }
    
    public static bool IsSimple(this Type type) =>
        TypeDescriptor.GetConverter(type).CanConvertFrom(typeof(string));
    
    public static object? GetInstance(this Type propertyPropertyType)
    {
        var ctorWithoutParameters = propertyPropertyType.GetConstructors()?.FirstOrDefault(s => s.GetParameters()?.Any() != true);
        if (ctorWithoutParameters == null)
            throw new ArgumentException($"Тип {propertyPropertyType.FullName} должен иметь конструктор без параметров.");
        
        return ctorWithoutParameters?.Invoke(null);
    }
}