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

    /// <summary>
    /// Меняет типы для примитивных типов + расширение для Guid и DateTime?
    /// </summary>
    /// <param name="value"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public static object? ChangeType(this object? value, Type? type) =>
        type switch
        {
            _ when type == null || value == null => null,
            _ when type == typeof(DateTimeOffset) ||
                   type == typeof(DateTimeOffset?) => 
                DateTimeOffset.TryParse(value.ToString()!, out var parsedDateTimeOffset)
                    ? parsedDateTimeOffset
                    : null,
            _ when type == typeof(Guid) ||
                   type == typeof(Guid?) => 
                Guid.TryParse(value.ToString()!, out var parsedGuid)
                    ? parsedGuid
                    : null,
            _ when type.GUID == typeof(Nullable<>).GUID => Convert.ChangeType(value.ToString(), type.GenericTypeArguments.Single()),
            _ => Convert.ChangeType(value.ToString(), type)
        };

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
    
    public static object? GetDefaultInstance(this Type t) => 
        t == typeof(string) ||
        (t.IsValueType &&
        Nullable.GetUnderlyingType(t) != null)
            ? null
            : Activator.CreateInstance(t, true);
}