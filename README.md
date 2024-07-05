## Модуль сравнения сущностей с идентификационным свойством

Функциональность представлена IDifferenceService<TId> и предоставляет следующие механизмы:
- Получение списка различий (класс Difference) - _GetDifferences(object primaryObj, object secondaryObj)_;
- Метод применения изменений - _Patch(object sourceObject, IEnumerable<Difference<TId>> differences)_.

### Требования к работе:
1) Как корневой объект, так и все вложенные должны иметь идентификационное свойство (ИС) с уникальным значением (по значению ИС производится идентификация изменений).

    По умолчанию имя ИС "Id"; возможно его изменение через вызов метода _Extensions.SetIdentificationPropertyName(string identificationPropertyName)_.

    На данный момент модуль поддерживает работу с ИС, имеющими одно название для всех объектов (например, нельзя для корневого объекта указать название ИС "Id", а для вложенного объекта "ObjectId").
2) Поддерживаются идентификаторы примитивных типов (например Int32), а также Guid.
3) Все классы, используемые в обрабатываемых объектах должны иметь конструкторы без параметров.
4) Поддерживаются только свойства.
5) Для использования коллекций/массивов требуется использовать generics (тестировалось на List<>).

### Типы изменений (Difference)
Существуют следующие типы изменений:
1) Изменение примитивного свойства
```
{
    "Id": 1,                                // Идентификатор изменения
    "EntityId": 1,                          // Идентификатор сущности, к которой применяется изменение
    "PropertyPath": "Name",                 // Название свойства для изменения
    "OldValue": "Продукт 1",                // Старое значение свойства 
    "NewValue": "Продукт 1 - поменяли имя", // Новое значение свойства
    "Childs": null                          // Список дочерних изменений - для данного типа изменения не используется
  }
```
2) Изменение вложенного свойства

<details>
  <summary>Пример создания/изменения объекта</summary>

```
{
    "Id": 1,                                // Идентификатор изменения
    "EntityId": 1,                          // Идентификатор сущности, к которой применяется изменение
    "PropertyPath": "License",              // Название свойства для изменения
    "OldValue": null,                       // Не используется 
    "NewValue": null,                       // Не используется
    "Childs": [                             // Заполняется список изменений
        {                                   
          "Id": 3,                          // Данное изменение особенное
          "EntityId": 0,                    // создаётся для заполнения свойства,
          "PropertyPath": "Id",             // которое было рано null - устанавливаем идентификатор свойства
          "OldValue": "0",                  
          "NewValue": "1",                  
          "Childs": null
        },
        {                                   // Далее идут свойства устанавливающие
          "Id": 4,                          // остальные свойства объекта
          "EntityId": 1,
          "PropertyPath": "Name",
          "OldValue": null,
          "NewValue": "Лицензия продукта 1 - обновили",
          "Childs": null
        },
        {
          "Id": 5,
          "EntityId": 1,
          "PropertyPath": "Type",
          "OldValue": null,
          "NewValue": "Ну просто лицензия продукта 1 - обновили",
          "Childs": null
        }
    ]
  }
```
</details>

<details>
  <summary>Пример удаления объекта</summary>

```
  {
    "Id": 6,
    "EntityId": 1,
    "PropertyPath": "Registration",
    "OldValue": null,
    "NewValue": null,
    "Childs": [
      {
        "Id": 7,                            // Сперва следуют обнуления всех свойств
        "EntityId": 0,                      // для сохранения историчности
        "PropertyPath": "Name",
        "OldValue": "Регистрацию удалим",
        "NewValue": null,
        "Childs": null
      },
      {
        "Id": 8,                            // Последнее изменение устанавливает Id
        "EntityId": 0,                      // объекта в значение default
        "PropertyPath": "Id",
        "OldValue": "0",
        "NewValue": "0",
        "Childs": null
      }
    ]
```
</details>

При работе с колекциями создаются изменения такого же рода - идентификация происходит по _EntityId_.

### TODO
1) Реализовать метод _Unpatch_ для отката изменений объекта по списку изменений.


2) ~~Реализовать в классе _Extensions_ _Dictionary<Type, string>()_, хранящий информацию о названии ИС по типу.~~

    ~~Также реализовать соответствующий метод _SetIdentificationName(this Type type, string identificationPropertyName)_ и модернизировать метод _FindIdPropertyAndThrow(--)_ для работы с введённым словарём.
    Данный механизм позволит задавать разные названия идентификационных свойств в зависимости от типа обрабатываемого объекта.~~

   ~~По такой же логике стоит реализовать поддержку разных типов идентификаторов в зависимости от типа.~~

   **Функциональность реализована в рамках [PR-2](https://github.com/Hizeqwane/DifferencesService/pull/2). Требуется добавить соответствующие тесты.**


3) Предусмотреть в тестах вариант обнуления и установки свойств-списков.
