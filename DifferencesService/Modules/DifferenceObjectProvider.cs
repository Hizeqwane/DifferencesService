using System.Collections;
using System.Reflection;
using DifferencesService.Interfaces;
using DifferencesService.Models;
using DifferencesService.Options;
using Newtonsoft.Json.Linq;

namespace DifferencesService.Modules;

public class DifferenceObjectProvider(
    IIdentificationService identificationService,
    DifferenceServiceOptions options)
    : IDifferenceObjectProvider
{
    private readonly bool _getEmptyProperties = options.GetEmptyProperties;

    public JToken? GetObjectWithDifferences(object? source, IEnumerable<Difference> differences) => 
        source == null
            ? null
            : GetObjectWithDifferencesInternal(source, differences, source.GetType());

    /// <summary>
    /// Получение JToken`а, содержащего изменения для всех конечных (вложенных примитивных) свойств 
    /// </summary>
    /// <param name="source">Объект без изменений</param>
    /// <param name="differences">Изменения, которые требуется отобразить</param>
    /// <param name="sourceType">Тип объекта</param>
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
            var isSimple = property.PropertyType.IsSimple();
            
            // Получаем значение
            var sourcePropertyValue = source != null
                ? property.GetValue(source)
                : null;
            
            // Ищем изменение этого свойства
            var difference = differencesList?.FirstOrDefault(s => s.PropertyPath == property.Name);
            
            // свойство не меняется
            if (difference == null || (isSimple && difference.NewValue.IsEqualsFromToString(difference.OldValue)))
            {
                jObj[property.Name] = GetJObjWithoutDifferences(sourcePropertyValue);
                
                continue;
            }
            
            // Текущее значение null - конструируем из differences
            if (sourcePropertyValue == null || (isSimple && difference is {NewValue: not null, OldValue: null}))
            {
                jObj[property.Name] = GetCtorJObj(difference, property.PropertyType);

                continue;
            }
            
            var elemType = property.PropertyType.GenericTypeArguments.FirstOrDefault();
            var isList = property.PropertyType.GetInterfaces().Contains(typeof(IList)) &&
                         elemType != null;
            // Свойство - список
            if (isList)
            {
                var sourcePropertyListValue = (sourcePropertyValue as IEnumerable)!
                    .OfType<object>()
                    .ToList();
                
                var elemIdProperty = identificationService.FindIdPropertyAndThrow(elemType!);

                if (difference.Childs == null)
                    throw new ArgumentException($"Попытка изменить свойство {property.Name} без дочерних изменений.");
                
                var differencesToRemove = difference.Childs
                    .Where(s => s.PropertyPath == elemIdProperty.Name &&
                                s.NewValue == null)
                    .ToList();
                
                var removeList = new List<object>();
                var notRemovingDict = new Dictionary<object, object>();
                foreach (var sourceValue in sourcePropertyListValue)
                {
                    var sourceValueId = elemIdProperty.GetValue(sourceValue);
                    var foundedRemoveDifference = differencesToRemove.FirstOrDefault(s => s.EntityId.IsEqualsFromToString(sourceValueId));
                    if (foundedRemoveDifference != null)
                        removeList.Add(sourceValue);
                    else
                        notRemovingDict.Add(sourceValueId!, sourceValue);
                }

                // Удалили все элементы
                if (removeList.Count == sourcePropertyListValue.Count)
                {
                    jObj[property.Name] = GetDeCtorJObj(sourcePropertyListValue);
                    
                    continue;
                }
                // Меняем коллекцию внутри

                var jArray = new JArray();
                
                var differencesIdsSet = difference.Childs
                    .Where(s => s.PropertyPath == elemIdProperty.Name &&
                                s.OldValue == null); 
                
                foreach (var addDif in differencesIdsSet) 
                    jArray.Add(
                        GetSimpleAddValue(
                            GetObjectWithDifferencesInternal(null,
                                difference.Childs
                                .Where(s =>
                                    s.Id == addDif.Id ||
                                    s.EntityId.IsEqualsFromToString(addDif.NewValue))
                                .ToList(),
                                elemType)
                        )!);
                
                foreach (var remove in removeList) 
                    jArray.Add(GetDeCtorJObj(remove)!);

                foreach (var notRemoving in notRemovingDict)
                {
                    var notRemovingDifferences = difference.Childs
                        .Where(s => s.EntityId.IsEqualsFromToString(notRemoving.Key))
                        .ToList();
                    
                    jArray.Add
                    (
                        GetSimpleValue
                        (
                            value: GetObjectWithDifferencesInternal
                            (
                                notRemoving.Value,
                                notRemovingDifferences,
                                elemType
                            )!, 
                            type: notRemovingDifferences.Count > 0
                                    ? DifferenceType.Change
                                    : DifferenceType.None
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
                // Нашли сброс Id ИЛИ для простого свойства установили null
                if (removeDifference != null || (isSimple && difference.NewValue == null))
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
        if (isSimple)
        {
            // Несовпадение OldValue и currentValue
            if ((isSimple && !difference.OldValue.IsEqualsFromToString(source)) ||
                (sourceType.IsArray &&
                 (source as IEnumerable)!.OfType<object>().GetArrayStrValue() != 
                 (difference.OldValue as IEnumerable)?.OfType<object>().GetArrayStrValue()))
                return GetSimpleValue(value: source);

            return GetSimpleValue(value: difference.NewValue, oldValue: source, type: DifferenceType.Change);
        }

        // Обработка массивов
        if (sourceType.IsArray)
        {
            var elemList = (source as IEnumerable)!
                .OfType<object>()
                .ToList();
            
            // Несовпадение OldValue и currentValue
            if ((sourceType.IsArray &&
                 elemList.GetArrayStrValue() != 
                 (difference.OldValue as IEnumerable)?.OfType<object>().GetArrayStrValue()))
                return GetSimpleValue(value: source);

            var newValueList = (difference.NewValue as IEnumerable)?.OfType<object>().ToList();
            var addList = newValueList?.Where(s => elemList.All(e => !e.IsEqualsFromToString(s)));
            var removingList = newValueList != null 
                ? elemList.Where(s => newValueList.All(n => !n.IsEqualsFromToString(s))).ToList()
                : elemList;
            
            var noneList = elemList.Where(s => removingList.All(r => !r.IsEqualsFromToString(s)));
            
            var jArray = new JArray();
            
            if (addList != null)
                foreach (var newValue in addList) 
                    jArray.Add(GetSimpleAddValue(newValue)!);
            
            foreach (var removingValue in removingList) 
                jArray.Add(GetSimpleRemoveValue(removingValue)!);
            
            foreach (var noneValue in noneList) 
                jArray.Add(GetSimpleValue(noneValue)!);
            
            
            return removingList.Count == elemList.Count
                ? GetSimpleRemoveValue(jArray)
                : GetSimpleValue(value: jArray, type: DifferenceType.Change);
        }
        
        return GetSimpleValue
        (
            value: GetObjectWithDifferencesInternal(source, difference.Childs, sourceType),
            type: DifferenceType.Change
        );
    }
    
    /// <summary>
    /// Метод деконструирования списка
    /// </summary>
    /// <param name="sourceList"></param>
    /// <returns></returns>
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
    private JToken? GetDeCtorJObj(object? source)
    {
        if (source == null)
            return GetSimpleValue();

        var objType = source.GetType();
        
        // Обработка простых типов
        if (objType.IsSimple())
            return GetSimpleRemoveValue(source);
        
        // Если объект - список или массив
        var sourceListValue = (source as IEnumerable)?
            .OfType<object>()
            .ToList();
        
        // Обработка массивов и списков
        if (sourceListValue != null)
            return GetDeCtorJObj(sourceListValue);
        
        var (jObj, _, _) = GetJObjectWithDifferences(source, null, objType);

        var idProperty = identificationService.FindIdPropertyAndThrow(objType);
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
        if (objType.IsSimple())
            return GetSimpleAddValue(difference.NewValue);
        
        // Обработка массивов
        if (objType.IsArray)
        {
            var elemList = (difference.NewValue as IEnumerable)?
                .OfType<object>()
                .ToList();

            if (elemList == null)
                return null;
            
            var jArray = new JArray();
            
            foreach (var arrayElem in elemList)
                jArray.Add(GetSimpleAddValue(arrayElem)!);

            return jArray;
        }
        
        if (difference.Childs == null)
            return null;

        var listElemType = objType.GenericTypeArguments.FirstOrDefault();
        // Сложный объект не список
        if (listElemType == null)
            return GetSimpleAddValue(
                GetObjectWithDifferencesInternal(null, difference.Childs, objType));
        
        var idProperty = identificationService.FindIdPropertyAndThrow(listElemType);

        // Группируем difference.Childs по созданию каждого объекта
        var difGroupList = new List<List<Difference>>();
        List<Difference> currentList = null!;
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
        if (objType.IsSimple())
            return GetSimpleValueWithoutDifferences(source);
        
        // Сложный объект список
        var elemList = (source as IEnumerable)?
            .OfType<object>()
            .ToList();
        
        // Обработка массивов
        if (objType.IsArray)
        {
            var jArray = new JArray();

            foreach (var arrayElem in elemList!) 
                jArray.Add(GetSimpleValueWithoutDifferences(arrayElem)!);

            return GetSimpleValueWithoutDifferences(jArray);
        }

        var listElemType = objType.GenericTypeArguments.FirstOrDefault();
        // Сложный объект не список
        if (listElemType == null)
            return GetSimpleValueWithoutDifferences(
                GetObjectWithDifferencesInternal(source, null, objType));

        if (elemList == null)
            return null;
        
        var jListToken = new JArray();
        foreach (var elem in elemList) 
            jListToken.Add(GetSimpleValueWithoutDifferences(
                GetObjectWithDifferencesInternal(elem, null, listElemType))!);
        
        return GetSimpleValueWithoutDifferences(jListToken);
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
        var idProperty = identificationService.FindIdPropertyAndThrow(sourceType);
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

    private JToken? SimpleTreatment(object? source, Difference? difference, Type sourceType)
    {
        if (difference == null ||                                           // Нет изменения 
            difference.OldValue.IsEqualsFromToString(source) ||             // OldValue != currentValue 
            (difference.NewValue == null && difference.OldValue == null))   // NewValue == null - изменение с null на null
            return GetSimpleValue(source);
            
        // Создание
        if (difference.OldValue == null)
            return GetSimpleAddValue(source);

        // Удаление
        if (difference.NewValue == null)
            return GetSimpleRemoveValue(source);

        // Изменение
        return GetSimpleValue
        (
            difference.NewValue.ChangeType(sourceType),
            difference.OldValue.ChangeType(sourceType),
            DifferenceType.Change
        );
    }

    private JToken? GetSimpleValueWithoutDifferences(object? value) =>
        GetSimpleValue(value);
    
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
}