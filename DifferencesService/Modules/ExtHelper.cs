using System.ComponentModel;
using System.Reflection;

namespace DifferencesService.Modules;

public static class ExtHelper
{
    public static bool IsEqualsFromToString(this object? primaryObjId, object? secondaryObjId) => 
        primaryObjId?.ToString() == secondaryObjId?.ToString();

    public static bool IsSimple(this Type type) =>
        TypeDescriptor.GetConverter(type).CanConvertFrom(typeof(string))
        || type == typeof(DateTime?)
        || type == typeof(Guid?);
    
    public static object? GetInstance(this Type propertyType)
    {
        var ctorWithoutParameters = propertyType.GetConstructors()?.FirstOrDefault(s => s.GetParameters()?.Any() != true);
        if (ctorWithoutParameters == null)
            throw new ArgumentException($"Тип {propertyType.FullName} должен иметь конструктор без параметров.");
        
        return ctorWithoutParameters?.Invoke(null);
    }

    /// <summary>
    /// Меняет типы для примитивных типов + расширение для Guid и DateTime?
    /// </summary>
    /// <param name="value"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public static object? ChangeType(this object? value, Type? type) =>
        type == null
        ? null 
        : value == null
            ? value
            : type == typeof(Guid)
                ? Guid.Parse(value.ToString()!)
                : type == typeof(Guid?)
                    ? Guid.Parse(value.ToString()!)
                    : type == typeof(DateTime?) 
                        ? Convert.ChangeType(value.ToString(), typeof(DateTime))
                        : Convert.ChangeType(value?.ToString(), type);

    public static string? GetArrayStrValue(this IEnumerable<object>? list) =>
        list != null
            ? string.Join("||", list)
            : null;

    public static void RemoveRangeByIds(this List<object>? valueList, IEnumerable<object> idList, PropertyInfo idProperty)
    {
        if (valueList?.Any() != true)
            return;

        foreach (var idToRemove in idList)
        {
            var founded = valueList.FirstOrDefault(s => idProperty.GetValue(s).IsEqualsFromToString(idToRemove));
            if (founded != null)
                valueList.Remove(founded);
        }
    }
}