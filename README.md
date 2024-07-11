## Модуль сравнения сущностей с идентификационным свойством

### Получение различий между двумя объектами

Функциональность представлена _IDifferenceHandler_ и предоставляет следующие механизмы:
- Получение списка различий (класс _Difference_) - **GetDifferences(object? primaryObj, object secondaryObj)**

  В качестве первого параметра можно передать null, тогда в результирующих _IEnumerable<Difference>_ будет инициализирующее свойство (установка ИС);
- Метод применения изменений к объекту - **Patch(object sourceObject, IEnumerable<Difference> differences)**;
- Метод **object Build(Type typeOfObject, IEnumerable<Difference> differences)** позволяющий по типу и списку изменений, содержащих инициализирующее свойство (установку ИС) получить объект.

Для подключения функциональности представлены методы расширения:
- **IServiceCollection UseDifferenceService<TId>(this IServiceCollection services)**
- **IServiceCollection UseDifferenceService<TId>(this IServiceCollection services, Func<DifferenceServiceOptions, DifferenceServiceOptions> setup)**

Второй метод позволяет конфигурировать:
- Наименование _идентификационного свойства_ (ИС) по умолчанию; используется для идентификации изменений объекта: **.WithDefaultIdPropertyName(string defaultIdPropertyName)**
- Наименование ИС для конкретного типа объекта: **.SetupIdPropertyNameForType<T>(string idPropertyName)**
- Задание провайдера идентификаторов для объектов _Difference_; по умолчанию используется _IntIdentificatorProvider_, который задаёт в качестве идентификаторов объектов _Difference_ целые натуральные числа; также в библиотеке присутствует _GuidIdentificatorProvider_, который задаёт в качестве идентификатора _Guid (Guid.NewGuid)_.

  Для задания собственного провайдера необходимо в метод расширения **SetupIdentificationProvider(IIdentificationProvider provider)** передать собственный провайдер.

### Требования к работе:
1) Как корневой объект, так и все вложенные должны иметь _идентификационное свойство_ (ИС) с уникальным значением (по значению ИС производится идентификация изменений).
2) Идентификаторы должны преобразовываться к строке, так как для сравнения используется приведение к _string_.
3) Все классы, используемые в обрабатываемых объектах должны иметь конструкторы без параметров (в случае, если свойство ссылочного типа равно _null_ будет создаваться экземпляр).
4) Поддерживаются только свойства.
5) Поддерживаются массивы (_Array_) только примитивных типов.
6) Для использования коллекций/массивов непримитивных объектов требуется использовать generics (тестировалось на _List<>_).

### Типы изменений (_Difference_)
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
  "Id": 3,                           // Идентификатор изменения
  "EntityId": 1,                     // Идентификатор сущности, к которой применяется изменение
  "PropertyPath": "License",         // Название свойства для изменения 
  "OldValue": null,
  "NewValue": null,
  "Childs": [                        // Список дочерних изменений
    {                                // Изменение специального вида
      "Id": 4,                       // Задаёт ИС для объекта, типа License 
      "EntityId": null,              // происходит создание экземпляра
      "PropertyPath": "Id",
      "OldValue": null,
      "NewValue": "11",
      "Childs": null
    },
    {
      "Id": 5,                       // Задание свойсва Name
      "EntityId": 11,
      "PropertyPath": "Name",
      "OldValue": null,
      "NewValue": "Имя лицензии",
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
  "Id": 3,                          // Идентификатор изменения
  "EntityId": 1,                    // Идентификатор сущности, к которой применяется изменение
  "PropertyPath": "License",        // Название свойства для изменения
  "OldValue": null,
  "NewValue": null,
  "Childs": [                       // Список дочерних изменений
    {
      "Id": 4,                      // Сброс (установка в null) свойства Name
      "EntityId": 11,               
      "PropertyPath": "Name",
      "OldValue": "Имя лицензии",
      "NewValue": null,
      "Childs": null
    },
    {
      "Id": 5,                      // Изменение специального вида
      "EntityId": 11,               // Установка ИС в null
      "PropertyPath": "Id",         // Происходит удаление
      "OldValue": "11",
      "NewValue": null,
      "Childs": null
    }
  ]
},
```
</details>

При работе с колекциями создаются изменения такого же рода - идентификация происходит по _EntityId_.

### Конструктор объекта с изменениями

Функциональность представлена _IDifferenceObjectProvider_ и предоставляет следующие механизмы:
- Получение объекта с изменениями как объект _JToken_ - **GetObjectWithDifferences(object source, IEnumerable<Difference> differences)**;
- Метод применения изменений к объекту - _Patch(object sourceObject, IEnumerable<Difference> differences)_.

С помощью метода **IServiceCollection UseDifferenceService<TId>(this IServiceCollection services, Func<DifferenceServiceOptions, DifferenceServiceOptions> setup)** можно сконфигурировать:
- поведение формирования объекта с изменениями: **.WithEmptyPropertiesBehaviour(bool getEmptyProperties)**.

  По умолчанию используется значение _true_: для свойства, которое не изменялось, со значением null будет создан объект
   ```
   {
     "Value": null,
     "OldValue": null,
     "Type": None
   }
   ```
  При задании значения _false_ вместо показанного объекта будет задано просто значение _null_.

В качетсве результата будет создан объект, на месте каждого свойства которого будет подъобъект следующего вида:
```
{
  "Value": <>,          // Текущее значение свойства - не заполняется, если свойство было удалено
  "OldValue": <>,       // Предыдущее значение свойства - заполняется, если свойство было удалено или изменено
  "Type": None          // Тип изменения: Add - добавление; Remove - удаление; Change - изменение; None - отсутствие изменения
}
```

<details>
  <summary>Пример: объект без изменений</summary>

```
{
  "Id": 1,
  "SomeValues": [
    111,
    222
  ],
  "Name": "Имя продукта",
  "License": {
    "Id": 11,
    "Name": "Имя лицензии"
  },
  "Documents": [
    {
      "Id": 101,
      "Name": "Имя документа",
      "Attachments": [
        {
          "Id": 1011,
          "Name": "Имя attachment"
        },
        {
          "Id": 1012,
          "Name": "Имя attachment"
        }
      ]
    },
    {
      "Id": 102,
      "Name": "Имя документа",
      "Attachments": [
        {
          "Id": 1021,
          "Name": "Имя attachment"
        },
        {
          "Id": 1022,
          "Name": "Имя attachment"
        }
      ]
    }
  ],
  "CreatingDate": "2024-07-11T12:14:44",
  "ModifiedDate": "2024-07-10T12:14:44",
  "CreatedBy": "d3d8d19d-0da3-4499-8999-df423dea804a",
  "ModifiedBy": "95c3ac7b-2e5a-4f77-9871-f70d9cf0c8a5"
}
```
</details>

<details>
  <summary>Пример: объект с изменениями</summary>

```
{
  "Id": 1,
  "SomeValues": {
    "Value": [
      {
        "Value": 333,
        "OldValue": null,
        "Type": "Add"
      },
      {
        "Value": null,
        "OldValue": 222,
        "Type": "Remove"
      },
      {
        "Value": 111,
        "OldValue": null,
        "Type": "None"
      }
    ],
    "OldValue": null,
    "Type": "Change"
  },
  "Name": {
    "Value": "Имя продукта - другое",
    "OldValue": "Имя продукта",
    "Type": "Change"
  },
  "License": {
    "Value": {
      "Id": 11,
      "Name": {
        "Value": "Имя лицензии - поменяли",
        "OldValue": "Имя лицензии",
        "Type": "Change"
      }
    },
    "OldValue": null,
    "Type": "Change"
  },
  "Documents": {
    "Value": [
      {
        "Value": {
          "Id": 103,
          "Name": {
            "Value": "Имя документа - добавили",
            "OldValue": null,
            "Type": "Add"
          },
          "Attachments": [
            {
              "Value": {
                "Id": 1031,
                "Name": {
                  "Value": "Имя attachment",
                  "OldValue": null,
                  "Type": "Add"
                }
              },
              "OldValue": null,
              "Type": "Add"
            },
            {
              "Value": {
                "Id": 1032,
                "Name": {
                  "Value": "Имя attachment",
                  "OldValue": null,
                  "Type": "Add"
                }
              },
              "OldValue": null,
              "Type": "Add"
            }
          ]
        },
        "OldValue": null,
        "Type": "Add"
      },
      {
        "Value": null,
        "OldValue": {
          "Id": 102,
          "Name": {
            "Value": null,
            "OldValue": "Имя документа",
            "Type": "Remove"
          },
          "Attachments": {
            "Value": null,
            "OldValue": [
              {
                "Value": null,
                "OldValue": {
                  "Id": 1021,
                  "Name": {
                    "Value": null,
                    "OldValue": "Имя attachment",
                    "Type": "Remove"
                  }
                },
                "Type": "Remove"
              },
              {
                "Value": null,
                "OldValue": {
                  "Id": 1022,
                  "Name": {
                    "Value": null,
                    "OldValue": "Имя attachment",
                    "Type": "Remove"
                  }
                },
                "Type": "Remove"
              }
            ],
            "Type": "Remove"
          }
        },
        "Type": "Remove"
      },
      {
        "Value": {
          "Id": 101,
          "Name": {
            "Value": "Имя документа - изменили",
            "OldValue": "Имя документа",
            "Type": "Change"
          },
          "Attachments": {
            "Value": [
              {
                "Value": {
                  "Id": 1013,
                  "Name": {
                    "Value": "Имя attachment",
                    "OldValue": null,
                    "Type": "Add"
                  }
                },
                "OldValue": null,
                "Type": "Add"
              },
              {
                "Value": null,
                "OldValue": {
                  "Id": 1012,
                  "Name": {
                    "Value": null,
                    "OldValue": "Имя attachment",
                    "Type": "Remove"
                  }
                },
                "Type": "Remove"
              },
              {
                "Value": {
                  "Id": 1011,
                  "Name": {
                    "Value": "Имя attachment",
                    "OldValue": null,
                    "Type": "None"
                  }
                },
                "OldValue": null,
                "Type": "None"
              }
            ],
            "OldValue": null,
            "Type": "Change"
          }
        },
        "OldValue": null,
        "Type": "Change"
      }
    ],
    "OldValue": null,
    "Type": "Change"
  },
  "CreatingDate": {
    "Value": "2024-07-11T12:14:44",
    "OldValue": null,
    "Type": "None"
  },
  "ModifiedDate": {
    "Value": null,
    "OldValue": "2024-07-10T12:14:44",
    "Type": "Remove"
  },
  "CreatedBy": {
    "Value": "317329de-8015-4615-968e-8bdb98687468",
    "OldValue": "d3d8d19d-0da3-4499-8999-df423dea804a",
    "Type": "Change"
  },
  "ModifiedBy": {
    "Value": null,
    "OldValue": "95c3ac7b-2e5a-4f77-9871-f70d9cf0c8a5",
    "Type": "Remove"
  }
}
```
</details>

### TODO
1) Реализовать метод _Unpatch_ для отката изменений объекта по списку изменений.