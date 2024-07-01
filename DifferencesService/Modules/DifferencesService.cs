using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using DifferencesService.Interfaces;

namespace DifferencesService.Modules;

public class DifferencesService<TId>(IIdentificatorProvider<TId> identificatorProvider) : IDifferenceService<TId>
{
    public void Patch(object sourceObject, IEnumerable<Difference<TId>> differences) => 
        InternalPatch(sourceObject, differences);

    public IEnumerable<Difference<TId>> GetDifferences(object primaryObj, object secondaryObj) => 
        InternalGetDifferences(primaryObj, secondaryObj);

    #region Internal patch

    private void InternalPatch(object sourceObject, IEnumerable<Difference<TId>> differences)
    {
        var typeOfObject = sourceObject.GetType();
        var properties = typeOfObject.GetProperties();

        var idProperty = typeOfObject.FindIdPropertyAndThrow(properties);
        var idPropertyValue = idProperty.GetValue(sourceObject);
        
        foreach (var difference in differences)
        {
            // Проверям по id - Если id = default, то меняем объект
            if (idPropertyValue != default &&
                difference.EntityId?.Equals(idPropertyValue) == false)
                continue;
            
            // Свойство не найдено
            var property = properties.FirstOrDefault(s => s.Name == difference.PropertyPath);
            if (property == null)
                continue;

            // Получаем значение
            // Если получили null и свойство не примитивное - создаём инстанс
            // !!! ВНИМАНИЕ !!! Объект должен иметь конструктор без параметров
            var propertyValue = property.GetValue(sourceObject);
            if (propertyValue == null &&
                !property.PropertyType.IsSimple())
            {
                propertyValue = property.PropertyType.GetInstance();
                property.SetValue(sourceObject, propertyValue);
            }

            // Если нет дочерних изменений - изменяем текущее свойство
            if (difference.Childs == null)
            {
                // Если текущее значение не null и
                // в изменении OldValue не null и
                // эти значения не совпадают - не применяем
                var currentValueStr = propertyValue?.ToString();
                if (!string.IsNullOrEmpty(currentValueStr) &&
                    difference.OldValue != null &&
                    currentValueStr != difference.OldValue)
                    continue;
                
                // Для Guid пришлось делать исключение...
                var valueForSetting = property.PropertyType == typeof(Guid)
                    ? Guid.Parse(difference.NewValue ?? Guid.Empty.ToString())
                    : Convert.ChangeType(difference.NewValue, property.PropertyType);
                
                property.SetValue(sourceObject, valueForSetting);
                
                // Если поменяли id - устанавливаем вложенный объект
                if (property.Name == Extensions.IdentificationPropertyName)
                    idPropertyValue = idProperty.GetValue(sourceObject);
                    
                continue;
            }

            // Обрабатываем вложенные свойства
            if (propertyValue is not IEnumerable)
            {
                // Если было найдено дочернее изменение Id на default - удаляем свойство
                if (difference.Childs
                        .FirstOrDefault(s => s.PropertyPath == Extensions.IdentificationPropertyName &&
                                             s.NewValue == default(TId)?.ToString()) != null)
                {
                    property.SetValue(sourceObject, null);
                    continue;
                }
                
                InternalPatch(propertyValue!, difference.Childs);
                property.SetValue(sourceObject, propertyValue);
                
                continue;
            }
            
            // Обрабатываем массивы непримитивных объектов
            ComplicatingCollectionHandle(property, propertyValue, difference);
        }
    }
    
    private void ComplicatingCollectionHandle(
        PropertyInfo property,
        object? propertyValue,
        Difference<TId> difference)
    {
        // Нечего менять
        if (difference.Childs == null)
            return;

        // Поддерживаем только generics (List<T>)
        var entityType = property.PropertyType.GenericTypeArguments?.FirstOrDefault();
        if (entityType == null)
            return;
        
        // Убеждаемся, что вложенная сущность имеет идентификационное свойство
        var idProperty = entityType.FindIdPropertyAndThrow();
        
        // Формируем список для обработки
        var propertyEntityValueList = (propertyValue as IEnumerable)
            ?.Cast<object>()
            .ToList();
        if (propertyEntityValueList == null)
            return;
        
        var (entitiesToAdd, entityIdsToRemove) = GetEntitiesCollectionDifferences
        (
            difference,
            propertyEntityValueList,
            entityType,
            idProperty
        );

        propertyEntityValueList.AddRange(entitiesToAdd);

        foreach (var idToRemove in entityIdsToRemove)
        {
            var entityToRemove = propertyEntityValueList.FirstOrDefault(s => idToRemove!.Equals(idProperty.GetValue(s))); 
            if (entityToRemove != null)
                propertyEntityValueList.Remove(entityToRemove);
        }

        var colType = typeof(List<>).MakeGenericType(entityType);
        
        var addMethod = colType.GetMethod("Add");
        var removeAtMethod = colType.GetMethod("RemoveAt");
        var findIndex = colType.GetMethod("FindIndex", types: [typeof(Predicate<>).MakeGenericType(entityType)]);

        foreach (var entityToAdd in entitiesToAdd) 
            addMethod?.Invoke(propertyValue, [entityToAdd]);
                
        foreach (var idToRemove in entityIdsToRemove)
        {
            var predParam = Expression.Parameter(entityType, "p");
            
            var left = Expression.Property(predParam, idProperty);
            var right = Expression.Constant(idToRemove, typeof(TId));
            var equality = Expression.Equal(left, right);

            var predicateType = typeof(Predicate<>).MakeGenericType(entityType);
            var lambda = Expression.Lambda(predicateType, equality, predParam);
            var compiled = lambda.Compile();
                    
            var foundedIndex = (int)(findIndex?.Invoke
            (
                propertyValue,
                [compiled]
            ) ?? -1);
            if (foundedIndex != -1)
                removeAtMethod?.Invoke(propertyValue, [foundedIndex]);
        }
                
                
        foreach (var propEntityValue in propertyEntityValueList)
        {
            InternalPatch
            (
                propEntityValue,
                difference.Childs
                    .Where(s => s.EntityId != null && 
                                s.EntityId!.Equals(idProperty.GetValue(propEntityValue)))
            );
        }
    }

    private static (List<object> EntitiesToAdd, List<TId> EntityIdsToRemove) GetEntitiesCollectionDifferences(
        Difference<TId> difference,
        List<object> propertyEntityValueList,
        Type entityType, 
        PropertyInfo idProperty)
    {
        // Ищем сущности для добавления
        var entitiesToAdd = difference.Childs
            !.Distinct(new DifferenceEntityIdDistinctor<TId>())
            .Where(s => s.EntityId.IsEqualsFromToString(default(TId)?.ToString()) && 
                propertyEntityValueList.All(p => s.EntityId?.Equals(idProperty.GetValue(p)) != true))
            .Select(s =>
            {
                var obj = entityType.GetInstance();
                if (obj != null)
                    idProperty.SetValue(obj, s.EntityId);
                        
                return obj;
            })
            .Where(s => s != null)
            .Select(s => s!)
            .ToList();

        // Ищем сущности для удаления
        var entityIdsToRemove = difference.Childs
            !.Where(s => s.NewValue == default(TId)?.ToString())
            .Select(s => s.EntityId)
            .ToList();
        
        return (entitiesToAdd, entityIdsToRemove);
    }

    #endregion Internal patch

    #region Internal differences provider

    private List<Difference<TId>> InternalGetDifferences(object primaryObj, object secondaryObj)
    {
        var typeOfObject = primaryObj.GetType();
        if (typeOfObject != secondaryObj.GetType())
            throw new ArgumentException($"Типы объектов не совпадают.");
        
        var properties = typeOfObject.GetProperties();
        var idProperty = typeOfObject.FindIdPropertyAndThrow(properties);
        
        var primaryObjIdPropertyValue = idProperty.GetValue(primaryObj);
        var secondaryObjIdPropertyValue = idProperty.GetValue(secondaryObj);
        if (!primaryObjIdPropertyValue.IsEqualsFromToString(secondaryObjIdPropertyValue))
            throw new ArgumentException($"У переданных объектов обнаружены разные значения идентификационных свойств.");

        var differencesList = new List<Difference<TId>>();
        
        foreach (var property in properties)
        {
            var primaryObjPropertyValue = property.GetValue(primaryObj);
            var secondaryObjPropertyValue = property.GetValue(secondaryObj);
            if (primaryObjPropertyValue == null && 
                secondaryObjPropertyValue == null)
                continue;
            
            if (property.PropertyType.IsSimple())
            {
                if (primaryObjPropertyValue.IsEqualsFromToString(secondaryObjPropertyValue))
                    continue;
                
                var newSimpleDifference = new Difference<TId>
                {
                    Id = identificatorProvider.GetNextId(),
                    EntityId = (TId)primaryObjIdPropertyValue!,
                    PropertyPath = property.Name,
                    OldValue = primaryObjPropertyValue?.ToString(),
                    NewValue = secondaryObjPropertyValue?.ToString(),
                    Childs = null
                };
                
                differencesList.Add(newSimpleDifference);
                
                continue;
            }

            var newDifference = new Difference<TId>
            {
                Id = identificatorProvider.GetNextId(),
                EntityId = (TId)primaryObjIdPropertyValue!,
                PropertyPath = property.Name,
                OldValue = null,
                NewValue = null,
                Childs = new List<Difference<TId>>()
            };
               
            // Добавляем свойство, которое было null
            if (primaryObjPropertyValue == null)
            {
                if (secondaryObjPropertyValue is not IEnumerable secondaryObjPropertyEnumValue)
                    newDifference.Childs.AddRange(GetCtorDifferences(secondaryObjPropertyValue!));
                else
                    newDifference.Childs.AddRange(secondaryObjPropertyEnumValue.OfType<object>().SelectMany(GetCtorDifferences));
            } 
            // Обнулили свойство
            else if (secondaryObjPropertyValue == null)
            {
                if (primaryObjPropertyValue is not IEnumerable primaryObjPropertyEnumValue)
                    newDifference.Childs.AddRange(GetDeCtorDifferences(primaryObjPropertyValue!));
                else
                    newDifference.Childs.AddRange(primaryObjPropertyEnumValue.OfType<object>().SelectMany(GetDeCtorDifferences));
            }
            // Свойство было изменено внутри
            else
            {
                if (primaryObjPropertyValue is not IEnumerable)
                {
                    newDifference.Childs.AddRange(InternalGetDifferences(primaryObjPropertyValue, secondaryObjPropertyValue));
                }
                
                if (primaryObjPropertyValue is IEnumerable primaryObjEnumerableValue &&
                    secondaryObjPropertyValue is IEnumerable secondaryObjEnumerableValue)
                {
                    var primaryObjListValue = primaryObjEnumerableValue.Cast<object>().ToList();
                    var secondaryObjListValue = secondaryObjEnumerableValue.Cast<object>().ToList();

                    var elemType = primaryObjListValue.FirstOrDefault()?.GetType() ??
                                   secondaryObjListValue.FirstOrDefault()?.GetType();
                    
                    // Обе колекции пустые
                    if (elemType == null)
                        continue;

                    var elemIdProperty = elemType.FindIdPropertyAndThrow();
                    
                    var (elemToAdd, elemToRemove, elemToChange) = GetElems(primaryObjListValue, secondaryObjListValue , elemIdProperty);
                    
                    newDifference.Childs.AddRange(elemToAdd.SelectMany(GetCtorDifferences));
                    newDifference.Childs.AddRange(elemToChange.SelectMany(s => InternalGetDifferences(s.PrimaryObj, s.SecondaryObj)));
                    newDifference.Childs.AddRange(elemToRemove.SelectMany(GetDeCtorDifferences));
                }
            }

            if (newDifference.Childs.Count != 0)
                differencesList.Add(newDifference);
        }

        return differencesList;
    }

    private static (List<object> ElemsToAdd, List<object> ElemsToRemove, List<(object PrimaryObj, object SecondaryObj)> ElemsToChange) 
        GetElems(List<object> primaryObjListValue, List<object> secondaryObjListValue, PropertyInfo elemIdProperty)
    {
        var elemToAdd = secondaryObjListValue
            .Where
            (
                primary => primaryObjListValue.All
                (
                    secondary => !elemIdProperty
                        .GetValue(secondary)
                        .IsEqualsFromToString(elemIdProperty.GetValue(primary))
                )
            )
            .ToList();
        
        var elemToRemove = primaryObjListValue
            .Where
            (
                primary => secondaryObjListValue.All
                (
                    secondary => !elemIdProperty
                        .GetValue(secondary)
                        .IsEqualsFromToString(elemIdProperty.GetValue(primary))
                )
            )
            .ToList();

        var elemToChangeFromPrimary = primaryObjListValue
            .Except(elemToRemove)
            .ToList();
        
        var elemToChangeFromSecondary = secondaryObjListValue
            .Except(elemToAdd)
            .ToList();

        var elemsToChange = elemToChangeFromPrimary
            .Select
            (
                primary =>
                {
                    var primaryIdValue = elemIdProperty.GetValue(primary);
                    
                    return (
                        primary,
                        elemToChangeFromSecondary.First
                        (
                            secondary => elemIdProperty.GetValue(secondary).IsEqualsFromToString(primaryIdValue)
                        )
                    );
                })
            .ToList();
        
        return (elemToAdd, elemToRemove, elemsToChange);
    }

    private List<Difference<TId>> GetCtorDifferences(object source)
    {
        var result = new List<Difference<TId>>();

        var typeOfObject = source.GetType();
        var idProperty = typeOfObject.FindIdPropertyAndThrow();
        var id = idProperty.GetValue(source);

        // Отдельно добавляем Id
        result.Add(new Difference<TId>
        {
            Id = identificatorProvider.GetNextId(),
            EntityId = default!,
            PropertyPath = idProperty.Name,
            OldValue = default(TId)?.ToString(),
            NewValue = id?.ToString(),
            Childs = null
        });
        
        foreach (var property in typeOfObject.GetProperties().Where(s => s.Name != idProperty.Name))
        {
            var propertyValue = property.GetValue(source);
            if (propertyValue == null)
                continue;
            
            if (property.PropertyType.IsSimple())
            {
                var dif = new Difference<TId>()
                {
                    Id = identificatorProvider.GetNextId(),
                    EntityId = (TId)id!,
                    PropertyPath = property.Name,
                    OldValue = null,
                    NewValue = propertyValue?.ToString(),
                    Childs = null
                };
                
                result.Add(dif);
                
                continue;
            }
            
            if (propertyValue is IEnumerable enumerableValue)
            {
                // Нашли пустой список
                var elemValues = enumerableValue.Cast<object>().ToList();
                if (elemValues.Count == 0)
                    continue;
                
                var enumerableDif = new Difference<TId>()
                {
                    Id = identificatorProvider.GetNextId(),
                    EntityId = (TId)id!,
                    PropertyPath = property.Name,
                    OldValue = null,
                    NewValue = null,
                    Childs = new List<Difference<TId>>()
                };
                
                foreach (var elemValue in elemValues) 
                    enumerableDif.Childs.AddRange(GetCtorDifferences(elemValue));
                
                result.Add(enumerableDif);
                
                continue;
            }
            
            var nestedDif = new Difference<TId>()
            {
                Id = identificatorProvider.GetNextId(),
                EntityId = (TId)id!,
                PropertyPath = property.Name,
                OldValue = null,
                NewValue = null,
                Childs = GetCtorDifferences(propertyValue)
            };
                
            result.Add(nestedDif);
        }
        
        return result;
    }
    
    private List<Difference<TId>> GetDeCtorDifferences(object source)
    {
        var result = new List<Difference<TId>>();

        var typeOfObject = source.GetType();
        var idProperty = typeOfObject.FindIdPropertyAndThrow();
        var id = idProperty.GetValue(source);

        foreach (var property in typeOfObject.GetProperties().Where(s => s.Name != idProperty.Name))
        {
            var propertyValue = property.GetValue(source);
            if (propertyValue == null)
                continue;
            
            if (property.PropertyType.IsSimple())
            {
                var dif = new Difference<TId>()
                {
                    Id = identificatorProvider.GetNextId(),
                    EntityId = (TId)id!,
                    PropertyPath = property.Name,
                    OldValue = propertyValue?.ToString(),
                    NewValue = null,
                    Childs = null
                };
                
                result.Add(dif);
                
                continue;
            }
            
            if (propertyValue is IEnumerable enumerableValue)
            {
                // Нашли пустой список
                var elemValues = enumerableValue.Cast<object>().ToList();
                if (elemValues.Count == 0)
                    continue;
                
                var enumerableDif = new Difference<TId>()
                {
                    Id = identificatorProvider.GetNextId(),
                    EntityId = (TId)id!,
                    PropertyPath = property.Name,
                    OldValue = null,
                    NewValue = null,
                    Childs = new List<Difference<TId>>()
                };
                
                foreach (var elemValue in elemValues) 
                    enumerableDif.Childs.AddRange(GetDeCtorDifferences(elemValue));
                
                continue;
            }
            
            var nestedDif = new Difference<TId>()
            {
                Id = identificatorProvider.GetNextId(),
                EntityId = (TId)id!,
                PropertyPath = property.Name,
                OldValue = null,
                NewValue = null,
                Childs = GetDeCtorDifferences(propertyValue)
            };
                
            result.Add(nestedDif);
        }
        
        // Отдельно добавляем Id
        result.Add(new Difference<TId>
        {
            Id = identificatorProvider.GetNextId(),
            EntityId = (TId)id!,
            PropertyPath = idProperty.Name,
            OldValue = id?.ToString(),
            NewValue = default(TId)?.ToString(),
            Childs = null
        });
        
        return result;
    }

    #endregion Internal differences provider
}