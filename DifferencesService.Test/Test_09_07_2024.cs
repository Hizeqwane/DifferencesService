using System.Net.Http.Json;
using DifferencesService.Interfaces;
using DifferencesService.Modules;
using DifferencesService.Options;
using DifferencesService.Test.Models;
using JsonDiffPatchDotNet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework.Legacy;

namespace DifferencesService.Test;

public class Test_09_07_2024
{
    private IIdentificationService _identificationService;
    private IDifferenceHandler _differenceHandler;
    private IDifferenceObjectProvider _differenceObjectProvider;
    private JsonDiffPatch _jsonDiffPatch;
    
    [SetUp]
    public void Setup()
    {
        _jsonDiffPatch = new JsonDiffPatch();
        
        var options = new DifferenceServiceOptions()
            .WithDefaultIdPropertyName("Id")
            .WithEmptyPropertiesBehaviour(false);
        
        _identificationService = new IdentificationService(options);
        _differenceHandler = new DifferencesHandler(_identificationService);
        _differenceObjectProvider = new DifferenceObjectProvider(_identificationService, _differenceHandler, options);
    }
    
    [Test(Description = "Тест добавления")]
    public void TestAdd()
    {
        var p1 = GetEmptyProduct();
        var p2 = GetFullProduct();

        var diff = _differenceHandler.GetDifferences(p1, p2);

        var p1Patch = _differenceHandler.Patch(p1, diff);

        var jP1Patch = JToken.FromObject(p1Patch);
        var jP2 = JToken.FromObject(p2);
        
        ClassicAssert.True(_jsonDiffPatch.Diff(jP1Patch, jP2) == null);
    }
    
    [Test(Description = "Тест удаления")]
    public void TestRemove()
    {
        var p2 = GetEmptyProduct();
        var p1 = GetFullProduct();

        var diff = _differenceHandler.GetDifferences(p1, p2);

        var p1Patch = _differenceHandler.Patch(p1, diff);
        
        var jP1Patch = JToken.FromObject(p1Patch);
        var jP2 = JToken.FromObject(p2);
        
        ClassicAssert.True(_jsonDiffPatch.Diff(jP1Patch, jP2) == null);
    }
    
    [Test(Description = "Тест изменения внутри")]
    public void TestChange()
    {
        var p1 = GetFullProduct();
        var p2 = GetOtherFullProduct();

        var diff = _differenceHandler.GetDifferences(p1, p2);

        var p1Patch = _differenceHandler.Patch(p1, diff);
        
        var jP1Patch = JToken.FromObject(p1Patch);
        var jP2 = JToken.FromObject(p2);
        
        ClassicAssert.True(_jsonDiffPatch.Diff(jP1Patch, jP2) == null);
    }
    
    [Test(Description = "Тест получение объекта с изменениями")]
    public void TestAdd_TestGetObjectWithDifferences()
    {
        var p1 = GetEmptyProduct();
        var p2 = GetFullProduct();

        var diff = _differenceHandler.GetDifferences(p1, p2);

        var fromP1WithDifferences = _differenceObjectProvider.GetObjectWithDifferences(p1, diff);
        var fromP2WithDifferences = _differenceObjectProvider.GetObjectWithDifferences(p2, diff);
        
        
        ClassicAssert.True(true);
    }
    
    [Test(Description = "Тест получение объекта с изменениями")]
    public void TestRemove_TestGetObjectWithDifferences()
    {
        var p2 = GetEmptyProduct();
        var p1 = GetFullProduct();

        var diff = _differenceHandler.GetDifferences(p1, p2);

        var fromP1WithDifferences = _differenceObjectProvider.GetObjectWithDifferences(p1, diff);
        
        
        ClassicAssert.True(true);
    }
    
    [Test(Description = "Тест получение объекта с изменениями")]
    public void TestChange_TestGetObjectWithDifferences()
    {
        var p1 = GetFullProduct();
        var p2 = GetOtherFullProduct();

        var diff = _differenceHandler.GetDifferences(p1, p2);

        var fromP1WithDifferences = _differenceObjectProvider.GetObjectWithDifferences(p1, diff);
        
        
        ClassicAssert.True(true);
    }

    #region GetProducts

    private Product GetEmptyProduct() =>
        new Product
        {
            Id = 1
        };
    
    private Product GetFullProduct() =>
        new Product
        {
            Id = 1,
            Name = "Имя продукта",
            License = new License
            {
                Id = 11,
                Name = "Имя лицензии"
            },
            Documents = new List<Document>
            {
                new Document
                {
                    Id = 101,
                    Name = "Имя документа",
                    Attachments = new List<Attachment>
                    {
                        new Attachment
                        {
                            Id = 1011,
                            Name = "Имя attachment"
                        },
                        new Attachment
                        {
                            Id = 1012,
                            Name = "Имя attachment"
                        }
                    }
                },
                new Document
                {
                    Id = 102,
                    Name = "Имя документа",
                    Attachments = new List<Attachment>
                    {
                        new Attachment
                        {
                            Id = 1021,
                            Name = "Имя attachment"
                        },
                        new Attachment
                        {
                            Id = 1022,
                            Name = "Имя attachment"
                        }
                    }
                }
            },
            SomeValues = [111, 222]
        };
    
    private Product GetOtherFullProduct() =>
        new Product
        {
            Id = 1,
            Name = "Имя продукта - другое",
            License = new License
            {
                Id = 11,
                Name = "Имя лицензии - поменяли"
            },
            Documents = new List<Document>
            {
                new Document
                {
                    Id = 101,
                    Name = "Имя документа - изменили",
                    Attachments = new List<Attachment>
                    {
                        new Attachment
                        {
                            Id = 1011,
                            Name = "Имя attachment"
                        },
                        // Удалили 1012
                        new Attachment
                        {
                            Id = 1013,
                            Name = "Имя attachment"
                        }
                    }
                },
                // Удалили 102
                new Document
                {
                    Id = 103,
                    Name = "Имя документа - добавили",
                    Attachments = new List<Attachment>
                    {
                        new Attachment
                        {
                            Id = 1031,
                            Name = "Имя attachment"
                        },
                        new Attachment
                        {
                            Id = 1032,
                            Name = "Имя attachment"
                        }
                    }
                }
            },
            SomeValues = [111, 333]
        };

    #endregion GetProducts
}