using System.ComponentModel;
using System.Reflection;

namespace DifferencesService.Modules;

public static class ExtHelper
{
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