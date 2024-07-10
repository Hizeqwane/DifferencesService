using System.Collections;
using System.Reflection;
using DifferencesService.Interfaces;
using DifferencesService.Models;
using DifferencesService.Options;
using Newtonsoft.Json.Linq;

namespace DifferencesService.Modules;

public class DifferenceObjectProvider : IDifferenceObjectProvider
{
    private readonly IIdentificationService _identificationService;
    private readonly IDifferenceHandler _differenceHandler;

    private readonly bool _getEmptyProperties;
    
    public DifferenceObjectProvider(
        IIdentificationService identificationService,
        IDifferenceHandler differenceHandler,
        DifferenceServiceOptions options)
    {
        _identificationService = identificationService;
        _differenceHandler = differenceHandler;
        _getEmptyProperties = options.GetEmptyProperties;
    }

    public JToken? GetObjectWithDifferences(object? source, IEnumerable<Difference> differences) => 
        source == null
            ? null
            : GetObjectWithDifferencesInternal(source, differences, source.GetType());

    /// <summary>
    /// Получение JToken`а, содержащего изменения для всех конечных (вложенных примитивных) свойств 
    /// </summary>
    /// <param name="source">не примитивный тип</param>
    /// <param name="differences"></param>
    /// <param name="sourceType"></param>
    /// <returns></returns>
    private JToken? GetObjectWithDifferencesInternal(
        object? source,
        IEnumerable<Difference>? differences,
        Type sourceType)
    {
        // Для простых полей
        if (sourceType.IsSimple())
            return SimpleTreatment(source, differences?.SingleOrDefault(), sourceType);
        
        // Создаём объект, который будем собирать

        var (jObj, differencesList, idProperty) = GetJObjectWithDifferences(source, differences, sourceType);
        
        var properties = sourceType.GetProperties();
        foreach (var property in properties.Where(s => s.Name != idProperty.Name))
        {
            // Получаем значение
            var sourcePropertyValue = source != null
                ? property.GetValue(source)
                : null;
            
            // Ищем изменение этого свойства
            var difference = differencesList?.FirstOrDefault(s => s.PropertyPath == property.Name);
            
            // свойство не меняется
            if (difference == null)
            {
                jObj[property.Name] = GetJObjWithoutDifferences(sourcePropertyValue);
                
                continue;
            }
            
            // Текущее значение null - конструируем из differences
            if (sourcePropertyValue == null)
            {
                jObj[property.Name] = GetCtorJObj(difference, property.PropertyType);

                continue;
            }
            
            var elemType = property.PropertyType.GenericTypeArguments.FirstOrDefault();
            // Свойство - список
            if (elemType != null)
            {
                var sourcePropertyListValue = (sourcePropertyValue as IEnumerable)!
                    .OfType<object>()
                    .ToList();

                var elemIdProperty = _identificationService.FindIdPropertyAndThrow(elemType);

                if (difference.Childs == null)
                    throw new ArgumentException($"Попытка изменить свойство {property.Name} без дочерних изменений.");
                
                var differencesToRemove = difference.Childs
                    .Where(s => s.PropertyPath == elemIdProperty.Name &&
                                s.NewValue == null)
                    .ToList();
                
                var removeList = new List<object>();
                var changeDict = new Dictionary<object, object>();
                foreach (var sourceValue in sourcePropertyListValue)
                {
                    var sourceValueId = elemIdProperty.GetValue(sourceValue);
                    var foundedRemoveDifference = differencesToRemove.FirstOrDefault(s => s.EntityId.IsEqualsFromToString(sourceValueId));
                    if (foundedRemoveDifference == null)
                        removeList.Add(sourceValue);
                    else
                        changeDict.Add(sourceValueId!, sourceValue);
                }

                // Удалили все элементы
                if (removeList.Count == sourcePropertyListValue.Count)
                {
                    jObj[property.Name] = GetDeCtorJObj(sourcePropertyListValue);
                    
                    continue;
                }
                // Меняем коллекцию внутри

                var jArray = new JArray();
                
                var differencesToAdd = difference.Childs
                    .Where(s => s.PropertyPath == elemIdProperty.Name &&
                                s.OldValue == null);
                
                foreach (var addDif in differencesToAdd) 
                    jArray.Add(GetCtorJObj(addDif, elemType)!);
                
                foreach (var remove in removeList) 
                    jArray.Add(GetDeCtorJObj(remove)!);

                foreach (var change in changeDict)
                {
                    jArray.Add
                    (
                        GetObjectWithDifferencesInternal
                        (
                            change.Value,
                            differencesList?.Where(s => s.EntityId.IsEqualsFromToString(change.Key)),
                            elemType
                        )!
                    );
                }
                
                jObj[property.Name] = GetSimpleValue(value: jArray, type: DifferenceType.Change);
                    
                continue;
            }
            // Свойство - не список
            else
            {
                var removeDifference = difference.Childs?.FirstOrDefault(s => s.PropertyPath == idProperty.Name &&
                                                                              s.NewValue == null);
                // Новое значение null
                if (removeDifference != null)
                {
                    jObj[property.Name] = GetDeCtorJObj(sourcePropertyValue);
                    
                    continue;
                }
                
                // Изменение свойства
                jObj[property.Name] = GetChangeJObj(sourcePropertyValue, difference, property.PropertyType);
                
                continue;
            }
            

        }

        return jObj;
    }

    private JToken? GetChangeJObj(object source, Difference difference, Type sourceType)
    {
        var isSimple = sourceType.IsSimple(); 
        if (isSimple || sourceType.IsArray)
        {
            // Несовпадение OldValue и currentValue
            if ((isSimple && !difference.OldValue.IsEqualsFromToString(source)) ||
                (sourceType.IsArray &&
                 (source as IEnumerable)!.OfType<object>().GetArrayStrValue() != 
                 (difference.OldValue as IEnumerable)?.OfType<object>().GetArrayStrValue()))
                return GetSimpleValue(value: source);

            return GetSimpleValue(value: difference.NewValue, oldValue: source, type: DifferenceType.Change);
        }
        
        return GetObjectWithDifferencesInternal(source, difference.Childs, sourceType);
    }
    
    /// <summary>
    /// Метод деконструирования списка
    /// </summary>
    /// <param name="sourceList"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private JToken? GetDeCtorJObj(List<object>? sourceList)
    {
        if (sourceList?.Any() != true)
            return null;
        
        var jListToken = new JArray();
        foreach (var elem in sourceList) 
            jListToken.Add(GetDeCtorJObj(elem)!);
        
        return GetSimpleRemoveValue(jListToken);
    }
    
    /// <summary>
    /// Метод деконструирования объекта
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private JToken? GetDeCtorJObj(object? source)
    {
        if (source == null)
            return GetSimpleValue();

        var objType = source.GetType();
        
        // Обработка простых типов
        if (objType.IsSimple() || objType.IsArray)
            return GetSimpleRemoveValue(source);

        // Если объект - коллекция
        var sourceListValue = (source as IEnumerable)?
            .OfType<object>()
            .ToList();
        if (sourceListValue != null)
            return GetDeCtorJObj(sourceListValue);
        
        var (jObj, _, _) = GetJObjectWithDifferences(source, null, objType);

        var idProperty = _identificationService.FindIdPropertyAndThrow(objType);
        foreach (var property in objType.GetProperties().Where(s => s.Name != idProperty.Name)) 
            jObj[property.Name] = GetDeCtorJObj(property.GetValue(source));
        
        return GetSimpleRemoveValue(jObj);
    }

    /// <summary>
    /// Конструирует объект по difference.
    /// </summary>
    /// <param name="difference"></param>
    /// <param name="objType"></param>
    /// <returns></returns>
    private JToken? GetCtorJObj(Difference difference, Type objType)
    {
        // Обработка простых типов
        if (objType.IsSimple() || objType.IsArray)
            return GetSimpleAddValue(difference.NewValue);
        
        if (difference.Childs == null)
            return null;

        var listElemType = objType.GenericTypeArguments.FirstOrDefault();
        // Сложный объект не список
        if (listElemType == null)
            return GetSimpleAddValue(
                GetObjectWithDifferencesInternal(null, difference.Childs, objType));
        
        var idProperty = _identificationService.FindIdPropertyAndThrow(listElemType);

        // Группируем difference.Childs по созданию каждого объекта
        var difGroupList = new List<List<Difference>>();
        List<Difference> currentList = null;
        foreach (var dif in difference.Childs)
        {
            if (dif.PropertyPath == idProperty.Name && dif.NewValue != null)
            {
                currentList = new List<Difference>{ dif };
                
                difGroupList.Add(currentList);
                
                continue;
            }
            
            currentList!.Add(dif);
        }

        var jListToken = new JArray();
        foreach (var differenceGroup in difGroupList) 
            jListToken.Add(GetSimpleAddValue(
                GetObjectWithDifferencesInternal(null, differenceGroup, listElemType))!);
        
        return jListToken;
    }
    
    /// <summary>
    /// Конструирование свойства без изменений
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    private JToken? GetJObjWithoutDifferences(object? source)
    {
        if (source == null)
            return GetSimpleValue();

        var objType = source.GetType();
        
        // Обработка простых типов
        if (objType.IsSimple() || objType.IsArray)
            return GetSimpleValueWithoutDifferences(source);

        var listElemType = objType.GenericTypeArguments.FirstOrDefault();
        // Сложный объект не список
        if (listElemType == null)
            return GetSimpleValueWithoutDifferences(
                GetObjectWithDifferencesInternal(source, null, objType));

        // Сложный объект список
        var elemList = (source as IEnumerable)?
            .OfType<object>()
            .ToList();

        if (elemList == null)
            return null;
        
        var jListToken = new JArray();
        foreach (var elem in elemList) 
            jListToken.Add(GetSimpleValueWithoutDifferences(
                GetObjectWithDifferencesInternal(elem, null, listElemType))!);
        
        return jListToken;
    }

    /// <summary>
    /// Создаёт собираемый JToken
    /// Если source == null - устанавливает Id из Differences
    /// Фильтрует differences по EntityId
    /// </summary>
    /// <param name="source"></param>
    /// <param name="rawDifferences"></param>
    /// <param name="sourceType"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    private (JToken JObj, List<Difference>? DifferencesList, PropertyInfo IdProperty) GetJObjectWithDifferences(
        object? source, IEnumerable<Difference>? rawDifferences, Type sourceType)
    {
        var idProperty = _identificationService.FindIdPropertyAndThrow(sourceType);
        var rawDifferencesList = rawDifferences?.ToList();
        
        JToken jObj;
        object? sourceId;
        
        if (source == null)
        {
            jObj = new JObject();

            var idSetDifference = rawDifferencesList?
                .FirstOrDefault(s => s.PropertyPath == idProperty.Name && s.NewValue != null);
            if (idSetDifference == null)
                throw new ArgumentException($"Попытка установить объект типа {sourceType.FullName} без установки идентификатора.");

            sourceId = Convert.ChangeType(idSetDifference.NewValue!, idProperty.PropertyType);
            
            // Идентификационное свойство добавляем как есть
            jObj[idProperty.Name] = JToken.FromObject(sourceId!);
        }
        else
        {
            jObj = JToken.FromObject(source);
            sourceId = idProperty.GetValue(source);
        }
        
        
        var differences = rawDifferencesList?
            .Where(s => s.EntityId.IsEqualsFromToString(sourceId))
            .ToList();

        return (jObj, differences, idProperty);
    }
    //
    // private void SingleElemTreatment(PropertyInfo property, Difference foundedDifference, JObject jObject, 
    //     object? propertyValue)
    // {
    //     var childObjectIdPropertyName = _identificationService.GetIdPropertyName(property.PropertyType);
    //     var childObjectIdProperty = _identificationService.FindIdPropertyAndThrow(property.PropertyType);
    //         
    //     // Если меняем Id, значит это удаление, или добавление объекта
    //     var idChangeDifference = foundedDifference.Childs!
    //         .FirstOrDefault(s => s.PropertyPath == childObjectIdPropertyName);
    //     if (idChangeDifference != null)
    //     {
    //         // создание
    //         if (idChangeDifference.OldValue == null)
    //         {
    //             AppendToJObj
    //             (
    //                 jObject,
    //                 property,
    //                 value: GetObjectWithDifferencesInternal(propertyValue, foundedDifference.Childs!),
    //                 type: DifferenceType.Add
    //             );
    //         }
    //         // удаление
    //         else if (idChangeDifference.NewValue == null)
    //         {
    //             AppendToJObj
    //             (
    //                 jObject,
    //                 property,
    //                 oldValue: GetObjectWithDifferencesInternal(propertyValue, foundedDifference.Childs!),
    //                 type: DifferenceType.Remove
    //             );
    //         }
    //                 
    //         return;
    //     }
    //             
    //     // Изменяем
    //     AppendToJObj
    //     (
    //         jObject,
    //         property,
    //         value: GetObjectWithDifferencesInternal(propertyValue, foundedDifference.Childs!),
    //         type: DifferenceType.Change
    //     );
    // }

    private static string GetDifferenceType(bool isAdd, bool isRemove, IEnumerable<JToken> childJObj) =>
        isAdd
            ? DifferenceType.Add
            : isRemove
                ? DifferenceType.Remove
                : childJObj.Any(s => s[nameof(DifferenceValue.Type)]?.ToString() != DifferenceType.None)
                    ? DifferenceType.Change
                    : DifferenceType.None;

    private JToken? SimpleTreatment(object? source, Difference? difference, Type sourceType)
    {
        if (difference == null ||                                           // Нет изменения 
            difference.OldValue.IsEqualsFromToString(source) ||             // OldValue != currentValue 
            (difference.NewValue == null && difference.OldValue == null))   // NewValue == null - изменение с null на null
            return GetSimpleValue(source);
            
        // Создание
        if (difference.OldValue == null)
            return GetSimpleValue(source, type: DifferenceType.Add);

        // Удаление
        if (difference.NewValue == null)
            return GetSimpleValue(oldValue: source, type: DifferenceType.Remove);

        // Изменение
        return GetSimpleValue
        (
            difference.NewValue.ChangeType(sourceType),
            difference.OldValue.ChangeType(sourceType),
            DifferenceType.Change
        );
    }

    private JToken? GetSimpleValueWithoutDifferences(object? value) =>
        GetSimpleValue(value, type: DifferenceType.Add);
    
    private JToken? GetSimpleAddValue(object? value) =>
        GetSimpleValue(value, type: DifferenceType.Add);
    
    private JToken? GetSimpleRemoveValue(object? value) =>
        GetSimpleValue(oldValue: value, type: DifferenceType.Remove);
    
    private JToken? GetSimpleValue(object? value = null, object? oldValue = null, string type = DifferenceType.None)
    {
        if (!_getEmptyProperties && value == null && oldValue == null)
            return null;
        
        var differenceValue = new DifferenceValue
        {
            Value = value,
            OldValue = oldValue,
            Type = type
        };
        
        return JToken.FromObject(differenceValue);
    }

    private void AppendToJObj(JObject jObject, PropertyInfo property, object? value = null,
        object? oldValue = null, string type = DifferenceType.None)
    {
        var differenceValueNone = new DifferenceValue
        {
            Value = value,
            OldValue = oldValue,
            Type = type
        };

        if (_getEmptyProperties || (differenceValueNone.Value != null || differenceValueNone.OldValue != null))
            jObject[property.Name] = differenceValueNone.GetToken();
    }
}