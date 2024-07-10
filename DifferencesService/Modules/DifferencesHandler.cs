using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using DifferencesService.Interfaces;
using DifferencesService.Models;
using Newtonsoft.Json;

namespace DifferencesService.Modules;

public class DifferencesHandler(IIdentificationService identificationService) : IDifferenceHandler
{
    public object Patch(object sourceObject, IEnumerable<Difference> differences) => 
        InternalPatch(sourceObject, differences);

    public IEnumerable<Difference> GetDifferences(object primaryObj, object secondaryObj) => 
        InternalGetDifferences(primaryObj, secondaryObj);

    #region Internal patch

    private object InternalPatch(object sourceObj, IEnumerable<Difference> differences)
    {
        var typeOfObject = sourceObj.GetType();
        
        var obj = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(sourceObj), typeOfObject)!;
        
        var properties = typeOfObject.GetProperties();

        var idProperty = identificationService.FindIdPropertyAndThrow(typeOfObject, properties);
        var idPropertyValue = idProperty.GetValue(obj);
        
        foreach (var difference in differences.Where(s => s.EntityId.IsEqualsFromToString(idPropertyValue)))
        {
            // Свойство не найдено
            var property = properties.FirstOrDefault(s => s.Name == difference.PropertyPath);
            if (property == null)
                continue;

            var propertyValue = GetPropertyValue(property, obj, difference);
            // Внутри GetPropertyValue уже установили значение
            if (property.PropertyType.IsSimple() || property.PropertyType.IsArray)
                continue;
            
            // Обрабатываем сложные объекты
            
            // Попытка получить список
            var valueList = (propertyValue as IEnumerable)?
                .OfType<object>()
                .ToList();
            
            // Свойство - не список
            if (valueList == null)
            {
                // Если было найдено дочернее изменение Id на default - удаляем свойство
                if (difference.Childs?.Any(s => s.PropertyPath == identificationService.GetIdPropertyName(property.PropertyType) &&
                                               s.NewValue == null) != false)
                {
                    property.SetValue(obj, null);
                    continue;
                }
                
                var newPropertyValue = InternalPatch(propertyValue!, difference.Childs);
                property.SetValue(obj, newPropertyValue);
                
                continue;
            }
            
            // Обрабатываем массивы непримитивных объектов
            ComplicatingCollectionHandle(obj, property, valueList, difference);
        }

        return obj;
    }

    /// <summary>
    /// Получаем значение свойства.
    /// Если свойство - сложный объект и равно null - инициализируем
    /// Если массив или простое свойство - устанавливает его
    /// </summary>
    /// <param name="property"></param>
    /// <param name="obj"></param>
    /// <param name="difference"></param>
    /// <returns></returns>
    private object? GetPropertyValue(PropertyInfo property, object obj, Difference difference)
    {
        // Получаем значение
        var propertyValue = property.GetValue(obj);

        // Если свойство примитивное - возвращаем что есть
        if (property.PropertyType.IsSimple())
        {
            // Сравниваем OldValue с текущим
            // если не равны - не изменяем
            if (difference.OldValue.IsEqualsFromToString(propertyValue))
                property.SetValue(obj, difference.NewValue.ChangeType(property.PropertyType));
            
            return propertyValue;
        }
        
        // Свойство - массив И текущее значение null
        if (property.PropertyType.IsArray)
        {
            // Проверка на OldValue == currentValue
            if ((difference.OldValue as IEnumerable)?.OfType<object>().GetArrayStrValue() != 
                ((propertyValue as IEnumerable)?.OfType<object>().GetArrayStrValue() ??
                null))
                return propertyValue;

            // Установили null
            if (difference.NewValue == null)
            {
                property.SetValue(obj, null);
                return null;
            }    
            
            // Собираем новый массив
            var elemType = property.PropertyType.GetElementType();
            
            var newValueArray = (difference.NewValue! as IEnumerable)!
                .OfType<object>()
                .Select(s => Convert.ChangeType(s, elemType!))
                .ToList();
                    
            if (newValueArray.Count == 0)
                return null;
            
            var array = Array.CreateInstance
            (
                elemType!,
                newValueArray.Count()
            );

            for (var i = 0; i < newValueArray.Count; i++)
                array.SetValue(newValueArray[i], i);
            
            property.SetValue(obj, array);

            return array;
        }
        
        if (propertyValue != null)
            return propertyValue;
        
        // Если получили null и свойство не примитивное - создаём инстанс
        // !!! ВНИМАНИЕ !!! Объект должен иметь конструктор без параметров
        propertyValue = property.PropertyType.GetInstance();

        try
        {
            var idProperty = identificationService.FindIdPropertyAndThrow(property.PropertyType);
            var idDifference = difference.Childs?.FirstOrDefault(s => s.OldValue == null &&
                                                                      s.PropertyPath == idProperty.Name);
            if (idDifference == null)
                throw new ArgumentException($"Для свойства {property.Name} не удалось найти дочернего изменения с установкой Id.");

            idProperty.SetValue(propertyValue, Convert.ChangeType(idDifference.NewValue, idProperty.PropertyType));

        }
        catch (IdPropertyNotFoundException)// Не нашли идентификационное свойство - наткнулись на список
        {}
        
        property.SetValue(obj, propertyValue);

        return propertyValue;
    }

    /// <summary>
    /// Метод обработки коллекция сложных объектов
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="property"></param>
    /// <param name="valueList"></param>
    /// <param name="difference"></param>
    private void ComplicatingCollectionHandle(
        object obj,
        PropertyInfo property,
        List<object> valueList,
        Difference difference)
    {
        // Нечего менять
        if (difference.Childs!.Count == 0)
            return;

        // Поддерживаем только generics (List<T>)
        var elemType = property.PropertyType.GenericTypeArguments?.FirstOrDefault();
        if (elemType == null)
            return;
        
        // Убеждаемся, что вложенная сущность имеет идентификационное свойство
        var idProperty = identificationService.FindIdPropertyAndThrow(elemType);
        
        // Формируем список для обработки
                
        var (elemsToAdd, elemIdsToRemove) = GetEntitiesCollectionDifferences(difference, elemType);

        valueList.AddRange(elemsToAdd);
        valueList.RemoveRangeByIds(elemIdsToRemove, idProperty);

        // В итоговой коллекции нет элементов
        if (valueList.Count == 0)
        {
            property.SetValue(obj, null);
            return;
        }
        
        for (var i = 0; i < valueList.Count; i++)
        {
            var id = idProperty.GetValue(valueList[i]);

            var afterPatch = InternalPatch
            (
                valueList[i],
                difference.Childs
                    .Where
                    (
                        s => s.EntityId != null! &&
                             s.EntityId!.IsEqualsFromToString(id)
                    )
            );

            valueList[i] = afterPatch;
        }
        
        var listType = typeof(List<>).MakeGenericType(elemType);
        var valueToSet = (IList)Activator.CreateInstance(listType)!;
        for (var i = 0; i < valueList.Count; i++) 
            valueToSet.Add(valueList[i]);

        property.SetValue(obj, valueToSet);
    }

    /// <summary>
    /// Ищем элементы для добавления (создаём инстансы)
    /// Ищем Id элементов для удаления
    /// </summary>
    /// <param name="difference"></param>
    /// <param name="elemType"></param>
    /// <returns></returns>
    private (List<object> ElemsToAdd, List<object> ElemIdsToRemove)
        GetEntitiesCollectionDifferences(Difference difference, Type elemType)
    {
        var elemIdProperty = identificationService.FindIdPropertyAndThrow(elemType);

        var elemsToAdd = difference.Childs!
            .Where(s => s.PropertyPath == elemIdProperty.Name &&
                        s.OldValue == null)
            .Select(s =>
            {
                var newElem = elemType.GetInstance();
                elemIdProperty.SetValue(newElem, Convert.ChangeType(s.NewValue, elemIdProperty.PropertyType));
                
                return newElem;
            })
            .ToList();

        var elemsToRemove = difference.Childs!
            .Where(s => s.PropertyPath == elemIdProperty.Name &&
                        s.NewValue == null)
            .Select(s => s.EntityId)
            .ToList();

        return (elemsToAdd, elemsToRemove)!;
    }

    #endregion Internal patch

    #region Internal differences provider

    private List<Difference> InternalGetDifferences(object primaryObj, object secondaryObj)
    {
        var typeOfObject = primaryObj.GetType();
        if (typeOfObject != secondaryObj.GetType())
            throw new ArgumentException($"Типы объектов не совпадают.");
        
        var properties = typeOfObject.GetProperties();
        var idProperty = identificationService.FindIdPropertyAndThrow(typeOfObject, properties);
        
        var primaryObjIdPropertyValue = idProperty.GetValue(primaryObj);
        var secondaryObjIdPropertyValue = idProperty.GetValue(secondaryObj);
        if (!primaryObjIdPropertyValue.IsEqualsFromToString(secondaryObjIdPropertyValue))
            throw new ArgumentException($"У переданных объектов обнаружены разные значения идентификационных свойств.");

        var differencesList = new List<Difference>();
        
        foreach (var property in properties.Where(s => s.Name != idProperty.Name))
        {
            var primaryObjPropertyValue = property.GetValue(primaryObj);
            var secondaryObjPropertyValue = property.GetValue(secondaryObj);
            if (primaryObjPropertyValue == null && 
                secondaryObjPropertyValue == null)
                continue;
            
            // Обработка простого свойства
            if (property.PropertyType.IsSimple())
            {
                // Если значения одинаковые
                if (primaryObjPropertyValue.IsEqualsFromToString(secondaryObjPropertyValue))
                    continue;
                
                var newSimpleDifference = new Difference
                {
                    Id = identificationService.GetNextId(),
                    EntityId = primaryObjIdPropertyValue!,
                    PropertyPath = property.Name,
                    OldValue = primaryObjPropertyValue?.ToString(),
                    NewValue = secondaryObjPropertyValue?.ToString(),
                    Childs = null
                };
                
                differencesList.Add(newSimpleDifference);
                
                continue;
            }

            var primaryList = (primaryObjPropertyValue as IEnumerable)?
                .OfType<object>()
                .ToList();
                
            var secondaryList = (secondaryObjPropertyValue as IEnumerable)?
                .OfType<object>()
                .ToList();

            // Собираем сложное изменение
            var newDifference = new Difference
            {
                Id = identificationService.GetNextId(),
                EntityId = primaryObjIdPropertyValue!,
                PropertyPath = property.Name,
                OldValue = null,
                NewValue = null,
                Childs = new List<Difference>()
            };
              
            // Сложное свойство - не коллекция
            // Было null (Здесь гарантировано primaryObjPropertyValue == null И secondaryObjPropertyValue != null)
            if (primaryObjPropertyValue == null && secondaryList == null)
                newDifference.Childs.AddRange(GetCtorDifferences(secondaryObjPropertyValue!));
            // Сложное свойство - не коллекция
            // Стало null (Здесь гарантировано primaryObjPropertyValue != null И secondaryObjPropertyValue == null)
            else if (secondaryObjPropertyValue == null && primaryList == null)
                newDifference.Childs.AddRange(GetDeCtorDifferences(primaryObjPropertyValue!));
            // Свойство было изменено внутри
            // (Здесь гарантировано primaryObjPropertyValue != null И secondaryObjPropertyValue != null)
            else
            {
                // Сложное свойство - не коллекция
                if (primaryList == null && secondaryList == null)
                    newDifference.Childs.AddRange(InternalGetDifferences(primaryObjPropertyValue!, secondaryObjPropertyValue!));
                // Сложное свойство - коллекция
                else
                {
                    var elemType = primaryList?.FirstOrDefault()?.GetType() ??
                                   secondaryList?.FirstOrDefault()?.GetType();
                    
                    // Обе колекции пустые
                    if (elemType == null)
                        continue;

                    // Коллекция состоит из примитивных типов
                    if (elemType.IsSimple())
                    {
                        newDifference.Childs = null;
                        newDifference.OldValue = primaryList;
                        newDifference.NewValue = secondaryList;
                        
                        differencesList.Add(newDifference);
                        continue;
                    }
                    
                    // Был null
                    if (primaryList == null)
                        newDifference.Childs.AddRange(secondaryList!.SelectMany(GetCtorDifferences));
                    // Стал null
                    else if (secondaryList == null)
                        newDifference.Childs.AddRange(primaryList.SelectMany(GetDeCtorDifferences));
                    // Изменение коллекции внутри 
                    else
                    {
                        // Коллекция из сложных объектов
                        var (elemToAdd, elemToRemove, elemToChange) = GetElems(primaryList!, secondaryList! , elemType);
                    
                        newDifference.Childs.AddRange(elemToAdd.SelectMany(GetCtorDifferences));
                        newDifference.Childs.AddRange(elemToChange.SelectMany(s => InternalGetDifferences(s.PrimaryObj, s.SecondaryObj)));
                        newDifference.Childs.AddRange(elemToRemove.SelectMany(GetDeCtorDifferences));   
                    }
                }
            }

            if (newDifference.Childs.Count != 0)
                differencesList.Add(newDifference);
        }

        return differencesList;
    }

    /// <summary>
    /// Метод для нахождения элементов для добавления, удаления и изменения
    /// </summary>
    /// <param name="primaryList"></param>
    /// <param name="secondaryList"></param>
    /// <param name="elemType"></param>
    /// <returns></returns>
    private (List<object> ElemsToAdd, List<object> ElemsToRemove, List<(object PrimaryObj, object SecondaryObj)> ElemsToChange) 
        GetElems(List<object> primaryList, List<object> secondaryList, Type elemType)
    {
        var elemIdProperty = identificationService.FindIdPropertyAndThrow(elemType);

        var primaryDict = primaryList
            .ToDictionary(s => elemIdProperty.GetValue(s)!, s => s);
        
        var secondaryDict = secondaryList
            .ToDictionary(s => elemIdProperty.GetValue(s)!, s => s);
        
        var elemsToAdd = secondaryDict
            .Where(secondary => !primaryDict.ContainsKey(secondary.Key))
            .Select(s => s.Value)
            .ToList();
        
        var elemsToRemove = primaryDict
            .Where(primary => !secondaryDict.ContainsKey(primary.Key))
            .Select(s => s.Value)
            .ToList();

        var idsToChange = primaryDict
            .Where(s => secondaryDict.ContainsKey(s.Key))
            .Select(s => s.Key);

        var elemsToChange = idsToChange
            .Select(s => (primaryDict[s], secondaryDict[s]))
            .ToList();
        
        return (elemsToAdd, elemsToRemove, elemsToChange);
    }

    /// <summary>
    /// Метод возвращает список изменений, позволяющий "построить" объект
    /// Используется, если был null
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    private IEnumerable<Difference> GetCtorDifferences(object source)
    {
        var typeOfObject = source.GetType();
        var idProperty = identificationService.FindIdPropertyAndThrow(typeOfObject);
        var id = idProperty.GetValue(source);

        // Отдельно добавляем Id
        yield return new Difference
        {
            Id = identificationService.GetNextId(),
            EntityId = null!,
            PropertyPath = idProperty.Name,
            OldValue = null,
            NewValue = id?.ToString(),
            Childs = null
        };

        foreach (var property in typeOfObject.GetProperties().Where(s => s.Name != idProperty.Name))
        {
            var propertyValue = property.GetValue(source);
            if (propertyValue == null)
                continue;

            // Простое свойство
            if (property.PropertyType.IsSimple())
            {
                yield return new Difference()
                {
                    Id = identificationService.GetNextId(),
                    EntityId = id!,
                    PropertyPath = property.Name,
                    OldValue = null,
                    NewValue = propertyValue?.ToString(),
                    Childs = null
                };

                continue;
            }

            // Свойство типа коллекция
            if (propertyValue is IEnumerable enumerableValue)
            {
                // Нашли пустой список
                var elemValues = enumerableValue.OfType<object>().ToList();
                if (elemValues.Count == 0)
                    continue;

                var enumerableDif = new Difference()
                {
                    Id = identificationService.GetNextId(),
                    EntityId = id!,
                    PropertyPath = property.Name,
                    OldValue = null,
                    NewValue = null,
                    Childs = new List<Difference>()
                };

                foreach (var elemValue in elemValues)
                    enumerableDif.Childs.AddRange(GetCtorDifferences(elemValue));

                yield return enumerableDif;

                continue;
            }

            // Сложное свойство (не коллекция)
            yield return new Difference()
            {
                Id = identificationService.GetNextId(),
                EntityId = id!,
                PropertyPath = property.Name,
                OldValue = null,
                NewValue = null,
                Childs = GetCtorDifferences(propertyValue)
                    .ToList()
            };
        }
    }
    
    /// <summary>
    /// Метод возвращает список изменений, позволяющий "удалить" объект
    /// Используется, если стало null
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    private IEnumerable<Difference> GetDeCtorDifferences(object source)
    {
        var typeOfObject = source.GetType();
        var idProperty = identificationService.FindIdPropertyAndThrow(typeOfObject);
        var id = idProperty.GetValue(source);

        foreach (var property in typeOfObject.GetProperties().Where(s => s.Name != idProperty.Name))
        {
            var propertyValue = property.GetValue(source);
            if (propertyValue == null)
                continue;
            
            // Простое свойство
            if (property.PropertyType.IsSimple())
            {
                yield return new Difference()
                {
                    Id = identificationService.GetNextId(),
                    EntityId = id!,
                    PropertyPath = property.Name,
                    OldValue = propertyValue?.ToString(),
                    NewValue = null,
                    Childs = null
                };
                
                continue;
            }
            
            // Коллекция
            if (propertyValue is IEnumerable enumerableValue)
            {
                // Нашли пустой список
                var elemValues = enumerableValue.OfType<object>().ToList();
                if (elemValues.Count == 0)
                    continue;
                
                var enumerableDif = new Difference()
                {
                    Id = identificationService.GetNextId(),
                    EntityId = id!,
                    PropertyPath = property.Name,
                    OldValue = null,
                    NewValue = null,
                    Childs = new List<Difference>()
                };
                
                foreach (var elemValue in elemValues) 
                    enumerableDif.Childs.AddRange(GetDeCtorDifferences(elemValue));
                
                yield return enumerableDif;
                
                continue;
            }
            
            // Сложный объект (не коллекция)
            yield return new Difference
            {
                Id = identificationService.GetNextId(),
                EntityId = id!,
                PropertyPath = property.Name,
                OldValue = null,
                NewValue = null,
                Childs = GetDeCtorDifferences(propertyValue)
                    .ToList()
            };
        }
        
        // Отдельно добавляем Id
        yield return new Difference
        {
            Id = identificationService.GetNextId(),
            EntityId = id!,
            PropertyPath = idProperty.Name,
            OldValue = id?.ToString(),
            NewValue = null,
            Childs = null
        };
    }

    #endregion Internal differences provider
}