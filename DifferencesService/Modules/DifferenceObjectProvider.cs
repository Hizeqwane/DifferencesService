using System.Collections;
using System.Net.Mail;
using System.Reflection;
using DifferencesService.Interfaces;
using DifferencesService.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DifferencesService.Modules;

public class DifferenceObjectProvider : IDifferenceObjectProvider
{
    private readonly IIdentificationService _identificationService;
    private readonly IDifferenceHandler _differenceHandler;

    public DifferenceObjectProvider(
        IIdentificationService identificationService,
        IDifferenceHandler differenceHandler)
    {
        _identificationService = identificationService;
        _differenceHandler = differenceHandler;
    }

    public JToken? GetObjectWithDifferences(object source, IEnumerable<Difference> differences, bool getEmptyProperties = true) => 
        GetObjectWithDifferencesInternal(source, differences, getEmptyProperties);

    private JToken? GetObjectWithDifferencesInternal(
        object? source,
        IEnumerable<Difference> differences,
        bool getEmptyProperties)
    {
        if (source == null)
            return null;

        var typeOfObject = source.GetType();

        var allDifferencesList = differences.ToList();
        
        // Для простых полей
        if (typeOfObject.IsSimple())
            return SimpleTreatment(source, allDifferencesList.FirstOrDefault());
        
        var jObject = new JObject();
        
        var properties = typeOfObject.GetProperties();
        var idProperty = _identificationService.FindIdPropertyAndThrow(typeOfObject);
        var sourceId = idProperty.GetValue(source);

        jObject[idProperty.Name] = JToken.FromObject(sourceId!);
        
        var differencesList = allDifferencesList
            .Where(s => s.EntityId.IsEqualsFromToString(sourceId))
            .ToList();
        
        var objAfterDifferences = _differenceHandler.Patch(source, differencesList);
        
        foreach (var property in properties.Where(s => s.Name != idProperty.Name))
        {
            // Получаем значение
            var sourceValue = property.GetValue(source);
            var afterDifferenceValue = property.GetValue(objAfterDifferences);
            
            var propertyValue = sourceValue ?? afterDifferenceValue;
            var isAdd = sourceValue == null && afterDifferenceValue != null;
            var isRemove = sourceValue != null && afterDifferenceValue == null;
            
            var propertyEnumValues = (propertyValue as IEnumerable)?.OfType<object>().ToList();
            var propertyElemType = propertyEnumValues?.FirstOrDefault()?.GetType();
            var idElemProperty = propertyElemType?.GetProperty(_identificationService.GetIdPropertyName(propertyElemType));
            
            var foundedDifference = differencesList.FirstOrDefault(s => s.PropertyPath == property.Name);
            // Свойство не меняется
            if (foundedDifference == null)
            {
                if (property.PropertyType.IsSimple())
                {
                    var value = property.GetValue(source) ?? property.GetValue(objAfterDifferences);
                    if (getEmptyProperties || value != null)
                        jObject[property.Name] = SimpleTreatment(value, null);
                }
                else
                {
                    if (propertyEnumValues == null)
                         AppendToJObj
                        (
                            jObject,
                            property,
                            GetObjectWithDifferencesInternal(property.GetValue(source), Enumerable.Empty<Difference>(), getEmptyProperties),
                            getEmptyProperties: getEmptyProperties
                        );
                    else
                    {
                        var jArrayWithoutDifferences = new JArray();
                        
                        foreach (var propertyEnumValue in propertyEnumValues)
                        {
                            var idElemValue = idElemProperty!.GetValue(propertyEnumValue);
                            
                            jArrayWithoutDifferences.Add(GetObjectWithDifferencesInternal(propertyEnumValue, differencesList.Where(s => s.EntityId.IsEqualsFromToString(idElemValue)), getEmptyProperties)!);
                        }
                        
                        AppendToJObj(jObject, property, jArrayWithoutDifferences, getEmptyProperties: getEmptyProperties);
                    }
                }

                continue;
            }
            
            // Изменение простого свойства
            if (foundedDifference.OldValue != null ||
                foundedDifference.NewValue != null)
            {
                jObject[property.Name] = SimpleTreatment(propertyValue, foundedDifference);
                
                continue;
            }
            
            // Изменение/добавление/удаление сложного объекта

            // Объект не является колекцией
            if (propertyEnumValues == null)
            {
                SingleElemTreatment(property, foundedDifference, jObject, propertyValue, getEmptyProperties);
                continue;
            }
            
            // Коллекция была null или стала null
            if (isAdd || isRemove)
            {
                var jArrayWithoutDifferences = new JArray();
                        
                foreach (var propertyEnumValue in propertyEnumValues)
                {
                    var idElemValue = idElemProperty!.GetValue(propertyEnumValue);
                            
                    jArrayWithoutDifferences.Add
                    (
                        GetObjectWithDifferencesInternal
                        (
                            propertyEnumValue,
                            foundedDifference.Childs!.Where(s => s.EntityId.IsEqualsFromToString(idElemValue)),
                            getEmptyProperties
                        )!
                    );
                }
                        
                AppendToJObj
                (
                    jObject,
                    property,
                    jArrayWithoutDifferences,
                    type: isAdd
                        ? DifferenceType.Add
                        : DifferenceType.Remove,
                    getEmptyProperties: getEmptyProperties
                );
                
                continue;
            }
            
            
            // Коллекция изменилась внутри
            var jArray = new JArray();

            var sourceEnumValueList = (sourceValue as IEnumerable)!.OfType<object>().ToList();
            var afterDifferencesEnumValueList = (afterDifferenceValue as IEnumerable)!.OfType<object>().ToList();

            var sourceElemDict = new Dictionary<string, object>();
            // Перебираем элементы объекта-источника
            // Потом нужно перебрать элементы после изменения для нахождения элементов, который были добавлены
            foreach (var sourceElem in sourceEnumValueList)
            {
                var sourceElemId = idElemProperty!.GetValue(sourceElem);
                // Ищем такой же элемент после изменений
                var afterDifferencesElem = afterDifferencesEnumValueList.FirstOrDefault(s =>
                    idElemProperty.GetValue(s).IsEqualsFromToString(sourceElemId));

                sourceElemDict.TryAdd(sourceElemId!.ToString()!, sourceElem);
                
                // Если null - значит элемент был удалён изменениями
                if (afterDifferencesElem == null)
                {
                    jArray.Add(GetSimpleValue
                        (
                            oldValue: GetObjectWithDifferencesInternal(sourceElem, foundedDifference.Childs!, getEmptyProperties),
                            type: DifferenceType.Remove
                        )!
                    );
                    
                    continue;
                }

                var childDifferences = foundedDifference.Childs!
                    .Where(s => s.EntityId.IsEqualsFromToString(sourceElemId))
                    .ToList();
                
                // Элемент изменился
                jArray.Add(GetSimpleValue
                    (
                        value: GetObjectWithDifferencesInternal(sourceElem, childDifferences, getEmptyProperties),
                        type: childDifferences.Count > 0
                            ? DifferenceType.Change
                            : DifferenceType.None
                    )!
                );
            }

            // Элементы были добавлены
            foreach (var afterDifferencesElem in afterDifferencesEnumValueList
                         .Where(s => !sourceElemDict.ContainsKey(idElemProperty!.GetValue(s)!.ToString()!)))
            {
                var afterDifferencesElemId = idElemProperty!.GetValue(afterDifferencesElem);
                
                var childDifferences = foundedDifference.Childs!
                    .Where(s => s.EntityId.IsEqualsFromToString(afterDifferencesElemId))
                    .ToList();

                var elemEmptyInstance = propertyElemType!.GetInstance();
                idElemProperty.SetValue(elemEmptyInstance, afterDifferencesElemId);
                
                jArray.Add(GetSimpleValue
                    (
                        value: GetObjectWithDifferencesInternal(elemEmptyInstance, childDifferences, getEmptyProperties),
                        type: DifferenceType.Add
                    )!
                );
            }
            
            AppendToJObj(jObject, property, jArray, type: DifferenceType.Change, getEmptyProperties: getEmptyProperties);
        }
        
        return jObject;
    }

    private void SingleElemTreatment(PropertyInfo property, Difference foundedDifference, JObject jObject, 
        object? propertyValue, bool getEmptyProperties)
    {
        var childObjectIdPropertyName = _identificationService.GetIdPropertyName(property.PropertyType);
        var childObjectIdProperty = _identificationService.FindIdPropertyAndThrow(property.PropertyType);
            
        // Если меняем Id, значит это удаление, или добавление объекта
        var idChangeDifference = foundedDifference.Childs!
            .FirstOrDefault(s => s.PropertyPath == childObjectIdPropertyName);
        if (idChangeDifference != null)
        {
            // создание
            if (idChangeDifference.OldValue == null)
            {
                AppendToJObj
                (
                    jObject,
                    property,
                    value: GetObjectWithDifferencesInternal(propertyValue, foundedDifference.Childs!, getEmptyProperties),
                    type: DifferenceType.Add,
                    getEmptyProperties: getEmptyProperties
                );
            }
            // удаление
            else if (idChangeDifference.NewValue == null)
            {
                AppendToJObj
                (
                    jObject,
                    property,
                    oldValue: GetObjectWithDifferencesInternal(propertyValue, foundedDifference.Childs!, getEmptyProperties),
                    type: DifferenceType.Remove,
                    getEmptyProperties: getEmptyProperties
                );
            }
                    
            return;
        }
                
        // Изменяем
        AppendToJObj
        (
            jObject,
            property,
            value: GetObjectWithDifferencesInternal(propertyValue, foundedDifference.Childs!, getEmptyProperties),
            type: DifferenceType.Change,
            getEmptyProperties: getEmptyProperties
        );
    }

    private static string GetDifferenceType(bool isAdd, bool isRemove, IEnumerable<JToken> childJObj) =>
        isAdd
            ? DifferenceType.Add
            : isRemove
                ? DifferenceType.Remove
                : childJObj.Any(s => s[nameof(DifferenceValue.Type)]?.ToString() != DifferenceType.None)
                    ? DifferenceType.Change
                    : DifferenceType.None;

    private static JToken? SimpleTreatment(object? source, Difference? difference)
    {
        // Нет изменений - значение null
        if (source == null)
            return GetSimpleValue();
        
        var type = source?.GetType();
        
        // Нет изменений
        if (difference == null || 
            (difference.NewValue == null && difference.OldValue == null))
            return GetSimpleValue(source.ChangeType(type));
            
        // Создание
        if (difference.OldValue == null)
            return GetSimpleValue(source, type: DifferenceType.Add);

        // Удаление
        if (difference.NewValue == null)
            return GetSimpleValue(oldValue: source, type: DifferenceType.Remove);

        // Изменение
        return GetSimpleValue
        (
            difference.NewValue.ChangeType(type),
            difference.OldValue.ChangeType(type),
            DifferenceType.Change
        );
    }

    private static JToken? GetSimpleValue(object? value = null, object? oldValue = null, string type = DifferenceType.None)
    {
        var differenceValue = new DifferenceValue
        {
            Value = value,
            OldValue = oldValue,
            Type = type
        };

        return JToken.FromObject(differenceValue);
    }

    private static void AppendToJObj(JObject jObject, PropertyInfo property, object? value = null,
        object? oldValue = null, string type = DifferenceType.None, bool getEmptyProperties = true)
    {
        var differenceValueNone = new DifferenceValue
        {
            Value = value,
            OldValue = oldValue,
            Type = type
        };

        if (getEmptyProperties || (differenceValueNone.Value != null || differenceValueNone.OldValue != null))
            jObject[property.Name] = differenceValueNone.GetToken();
    }
}