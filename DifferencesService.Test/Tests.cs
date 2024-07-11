using System.Runtime.CompilerServices;
using DifferencesService.Interfaces;
using DifferencesService.Modules;
using DifferencesService.Options;
using DifferencesService.Test.Models;
using JsonDiffPatchDotNet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework.Legacy;

namespace DifferencesService.Test;

public class Tests
{
    private IIdentificationService _identificationService;
    private IDifferenceHandler _differenceHandler;
    private IDifferenceObjectProvider _differenceObjectProvider;
    private JsonDiffPatch _jsonDiffPatch;

    private JToken _p1ToP2WithDifference;
    private JToken _p1ToP3WithDifference;
    
    private JToken _p2ToP1WithDifference;
    private JToken _p3ToP1WithDifference;
    
    private JToken _p2ToP3WithDifference;
    private JToken _p3ToP2WithDifference;
    
    private JToken _p1WithoutDifference;
    private JToken _p2WithoutDifference;
    private JToken _p3WithoutDifference;
    
    [SetUp]
    public void Setup()
    {
        _jsonDiffPatch = new JsonDiffPatch();
        
        var options = new DifferenceServiceOptions()
            .WithDefaultIdPropertyName("Id")
            .WithEmptyPropertiesBehaviour(true);
        
        _identificationService = new IdentificationService(options);
        _differenceHandler = new DifferencesHandler(_identificationService);
        _differenceObjectProvider = new DifferenceObjectProvider(_identificationService, options);

        _p1ToP2WithDifference = Get_p1ToP2WithDifference();
        _p1ToP3WithDifference = Get_p1ToP3WithDifference();
        
        _p2ToP1WithDifference = Get_p2ToP1WithDifference();
        _p3ToP1WithDifference = Get_p3ToP1WithDifference();
        
        _p2ToP3WithDifference = Get_p2ToP3WithDifference();
        _p3ToP2WithDifference = Get_p3ToP2WithDifference();
        
        _p1WithoutDifference = Get_p1WithoutDifference();
        _p2WithoutDifference = Get_p2WithoutDifference();
        _p3WithoutDifference = Get_p3WithoutDifference();
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
        var p1 = GetEmptyProduct();
        var p2 = GetFullProduct();

        var diff = _differenceHandler.GetDifferences(p2, p1);

        var p2Patch = _differenceHandler.Patch(p2, diff);
        
        var jP2Patch = JToken.FromObject(p2Patch);
        var jP1 = JToken.FromObject(p1);
        
        ClassicAssert.True(_jsonDiffPatch.Diff(jP2Patch, jP1) == null);
    }
    
    [Test(Description = "Тест изменения внутри")]
    public void TestChange()
    {
        var p1 = GetEmptyProduct();
        var p2 = GetFullProduct();
        var p3 = GetOtherFullProduct();

        var diffP2ToP1 = _differenceHandler.GetDifferences(p2, p1).ToList();
        var diffP3ToP1 = _differenceHandler.GetDifferences(p3, p1).ToList();
        
        var diffP1ToP2 = _differenceHandler.GetDifferences(p1, p2).ToList();
        var diffP3ToP2 = _differenceHandler.GetDifferences(p3, p2).ToList();
        
        var diffP1ToP3 = _differenceHandler.GetDifferences(p1, p3).ToList();
        var diffP2ToP3 = _differenceHandler.GetDifferences(p2, p3).ToList();

        var p2PatchToP1 = _differenceHandler.Patch(p2, diffP2ToP1);
        var p3PatchToP1 = _differenceHandler.Patch(p3, diffP3ToP1);
        
        var p1PatchToP2 = _differenceHandler.Patch(p1, diffP1ToP2);
        var p3PatchToP2 = _differenceHandler.Patch(p3, diffP3ToP2);
        
        var p1PatchToP3 = _differenceHandler.Patch(p1, diffP1ToP3);
        var p2PatchToP3 = _differenceHandler.Patch(p2, diffP2ToP3);
        
        var p1PatchToP2PatchToP3 = _differenceHandler.Patch(p1PatchToP2, diffP2ToP3);
        var p1PatchToP3PatchToP2 = _differenceHandler.Patch(p1PatchToP3, diffP3ToP2);
        
        var p2PatchToP1PatchToP3 = _differenceHandler.Patch(p2PatchToP1, diffP1ToP3);
        var p2PatchToP3PatchToP1 = _differenceHandler.Patch(p2PatchToP3, diffP3ToP1);
        
        var p3PatchToP1PatchToP2 = _differenceHandler.Patch(p3PatchToP1, diffP1ToP2);
        var p3PatchToP2PatchToP1 = _differenceHandler.Patch(p3PatchToP2, diffP2ToP1);

        var jp1 = JToken.FromObject(p1);
        var jp2 = JToken.FromObject(p2);
        var jp3 = JToken.FromObject(p3);
        
        var jp2PatchToP1 = JToken.FromObject(p2PatchToP1);
        var jp3PatchToP1 = JToken.FromObject(p3PatchToP1);
        var jp1PatchToP2 = JToken.FromObject(p1PatchToP2);
        var jp3PatchToP2 = JToken.FromObject(p3PatchToP2);
        var jp1PatchToP3 = JToken.FromObject(p1PatchToP3);
        var jp2PatchToP3 = JToken.FromObject(p2PatchToP3);
        
        var jp1PatchToP2PatchToP3 = JToken.FromObject(p1PatchToP2PatchToP3);
        var jp1PatchToP3PatchToP2 = JToken.FromObject(p1PatchToP3PatchToP2);
        var jp2PatchToP1PatchToP3 = JToken.FromObject(p2PatchToP1PatchToP3);
        var jp2PatchToP3PatchToP1 = JToken.FromObject(p2PatchToP3PatchToP1);
        var jp3PatchToP1PatchToP2 = JToken.FromObject(p3PatchToP1PatchToP2);
        var jp3PatchToP2PatchToP1 = JToken.FromObject(p3PatchToP2PatchToP1);
        
        ClassicAssert.True(_jsonDiffPatch.Diff(jp2PatchToP1, jp1) == null);
        ClassicAssert.True(_jsonDiffPatch.Diff(jp3PatchToP1, jp1) == null);
        
        ClassicAssert.True(_jsonDiffPatch.Diff(jp1PatchToP2, jp2) == null);
        ClassicAssert.True(_jsonDiffPatch.Diff(jp3PatchToP2, jp2) == null);
        
        ClassicAssert.True(_jsonDiffPatch.Diff(jp2PatchToP3, jp3) == null);
        ClassicAssert.True(_jsonDiffPatch.Diff(jp1PatchToP3, jp3) == null);
        
        ClassicAssert.True(_jsonDiffPatch.Diff(jp1PatchToP2PatchToP3, jp3) == null);
        ClassicAssert.True(_jsonDiffPatch.Diff(jp1PatchToP3PatchToP2, jp2) == null);
        ClassicAssert.True(_jsonDiffPatch.Diff(jp2PatchToP1PatchToP3, jp3) == null);
        ClassicAssert.True(_jsonDiffPatch.Diff(jp2PatchToP3PatchToP1, jp1) == null);
        ClassicAssert.True(_jsonDiffPatch.Diff(jp3PatchToP1PatchToP2, jp2) == null);
        ClassicAssert.True(_jsonDiffPatch.Diff(jp3PatchToP2PatchToP1, jp1) == null);
    }
    
    [Test(Description = "Тест объектов без изменений")]
    public void TestWithoutChange()
    {
        var p1 = GetEmptyProduct();
        var p2 = GetFullProduct();
        var p3 = GetOtherFullProduct();

        var diffP1 = _differenceHandler.GetDifferences(p1, p1);
        var diffP2 = _differenceHandler.GetDifferences(p2, p2);
        var diffP3 = _differenceHandler.GetDifferences(p3, p3);
        
        ClassicAssert.True(!diffP1.Any() && !diffP2.Any() && !diffP3.Any());
    }
    
    [Test(Description = "Тест получение объекта с изменениями")]
    public void TestAdd_TestGetObjectWithDifferences()
    {
        var p1 = GetEmptyProduct();
        var p2 = GetFullProduct();
        var p3 = GetOtherFullProduct();

        var diffP1ToP2 = _differenceHandler.GetDifferences(p1, p2);
        var diffP1ToP3 = _differenceHandler.GetDifferences(p1, p3);

        var fromP1ToP2WithDifferences = _differenceObjectProvider.GetObjectWithDifferences(p1, diffP1ToP2);
        var fromP1ToP3WithDifferences = _differenceObjectProvider.GetObjectWithDifferences(p1, diffP1ToP3);
        
        var jsonDiffP1ToP2 = _jsonDiffPatch.Diff
        (
            JToken.Parse(JsonConvert.SerializeObject(fromP1ToP2WithDifferences)),
            _p1ToP2WithDifference
        );

        var jsonDiffP1ToP3 = _jsonDiffPatch.Diff
        (
            JToken.Parse(JsonConvert.SerializeObject(fromP1ToP3WithDifferences)),
            _p1ToP3WithDifference
        );
        
        ClassicAssert.True(jsonDiffP1ToP2 == null);
        ClassicAssert.True(jsonDiffP1ToP3 == null);
    }
    
    [Test(Description = "Тест получение объекта с изменениями")]
    public void TestRemove_TestGetObjectWithDifferences()
    {
        var p1 = GetEmptyProduct();
        var p2 = GetFullProduct();
        var p3 = GetOtherFullProduct();

        var diffP2ToP1 = _differenceHandler.GetDifferences(p2, p1);
        var diffP3ToP1 = _differenceHandler.GetDifferences(p3, p1);

        var fromP2ToP1WithDifferences = _differenceObjectProvider.GetObjectWithDifferences(p2, diffP2ToP1);
        var fromP3ToP1WithDifferences = _differenceObjectProvider.GetObjectWithDifferences(p3, diffP3ToP1);

        var jsonDiffP2ToP1 = _jsonDiffPatch.Diff
        (
            JToken.Parse(JsonConvert.SerializeObject(fromP2ToP1WithDifferences)),
            _p2ToP1WithDifference
        );

        var jsonDiffP3ToP1 = _jsonDiffPatch.Diff
        (
            JToken.Parse(JsonConvert.SerializeObject(fromP3ToP1WithDifferences)),
            _p3ToP1WithDifference
        );
        
        ClassicAssert.True(jsonDiffP2ToP1 == null);
        ClassicAssert.True(jsonDiffP3ToP1 == null);
    }
    
    [Test(Description = "Тест получение объекта с изменениями")]
    public void TestChange_TestGetObjectWithDifferences()
    {
        var p2 = GetFullProduct();
        var p3 = GetOtherFullProduct();

        var diffP2ToP3 = _differenceHandler.GetDifferences(p2, p3);
        var diffP3ToP2 = _differenceHandler.GetDifferences(p3, p2);
        
        var fromP2ToP3WithDifferences = _differenceObjectProvider.GetObjectWithDifferences(p2, diffP2ToP3);
        var fromP3ToP2WithDifferences = _differenceObjectProvider.GetObjectWithDifferences(p3, diffP3ToP2);

        var jsonDiffP2ToP3 = _jsonDiffPatch.Diff
        (
            JToken.Parse(JsonConvert.SerializeObject(fromP2ToP3WithDifferences)),
            _p2ToP3WithDifference
        );

        var jsonDiffP3ToP2 = _jsonDiffPatch.Diff
        (
            JToken.Parse(JsonConvert.SerializeObject(fromP3ToP2WithDifferences)),
            _p3ToP2WithDifference
        );
        
        ClassicAssert.True(jsonDiffP2ToP3 == null);
        ClassicAssert.True(jsonDiffP3ToP2 == null);
    }
    
    [Test(Description = "Тест получение объекта без изменений")]
    public void TestWithoutChanges_TestGetObjectWithDifferences()
    {
        var p1 = GetEmptyProduct();
        var p2 = GetFullProduct();
        var p3 = GetOtherFullProduct();

        var diffP1ToP1 = _differenceHandler.GetDifferences(p1, p1);
        var diffP2ToP2 = _differenceHandler.GetDifferences(p2, p2);
        var diffP3ToP3 = _differenceHandler.GetDifferences(p3, p3);

        var fromP1WithoutDifferences = _differenceObjectProvider.GetObjectWithDifferences(p1, diffP1ToP1);
        var fromP2WithoutDifferences = _differenceObjectProvider.GetObjectWithDifferences(p2, diffP2ToP2);
        var fromP3WithoutDifferences = _differenceObjectProvider.GetObjectWithDifferences(p3, diffP3ToP3);

        var jsonDiffP1ToP1 = _jsonDiffPatch.Diff
        (
            JToken.Parse(JsonConvert.SerializeObject(fromP1WithoutDifferences)),
            _p1WithoutDifference
        );
        
        var jsonDiffP2ToP2 = _jsonDiffPatch.Diff
        (
            JToken.Parse(JsonConvert.SerializeObject(fromP2WithoutDifferences)),
            _p2WithoutDifference
        );

        var jsonDiffP3ToP3 = _jsonDiffPatch.Diff
        (
            JToken.Parse(JsonConvert.SerializeObject(fromP3WithoutDifferences)),
            _p3WithoutDifference
        );
        
        ClassicAssert.True(jsonDiffP1ToP1 == null);
        ClassicAssert.True(jsonDiffP2ToP2 == null);
        ClassicAssert.True(jsonDiffP3ToP3 == null);
    }
    
    [Test(Description = "Тест получения инициализирующих изменений")]
    public void TestInitializingDifferences()
    {
        var p1 = GetEmptyProduct();
        var p2 = GetFullProduct();
        var p3 = GetOtherFullProduct();

        var diffP1ToNull= _differenceHandler.GetDifferences(null, p1);
        var diffP2ToNull = _differenceHandler.GetDifferences(null, p2);
        var diffP3ToNull = _differenceHandler.GetDifferences(null, p3);

        var newP1 = _differenceHandler.Build(p1.GetType(), diffP1ToNull);
        var newP2 = _differenceHandler.Build(p1.GetType(), diffP2ToNull);
        var newP3 = _differenceHandler.Build(p1.GetType(), diffP3ToNull);
        
        var jsonDiffP1ToNewP1 = _jsonDiffPatch.Diff
        (
            JToken.FromObject(p1),
            JToken.FromObject(newP1)
        );
        
        var jsonDiffP2ToNewP2 = _jsonDiffPatch.Diff
        (
            JToken.FromObject(p2),
            JToken.FromObject(newP2)
        );
        
        var jsonDiffP3ToNewP3 = _jsonDiffPatch.Diff
        (
            JToken.FromObject(p3),
            JToken.FromObject(newP3)
        );
        
        ClassicAssert.True(jsonDiffP1ToNewP1 == null);
        ClassicAssert.True(jsonDiffP2ToNewP2 == null);
        ClassicAssert.True(jsonDiffP3ToNewP3 == null);
    }

    #region GetProducts

    private Product GetEmptyProduct() =>
        new Product
        {
            Id = 1,
            CreatingDate = default,
            CreatedBy = default
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
            SomeValues = [111, 222],
            CreatingDate = DateTime.Parse("11.07.2024 12:14:44"),
            ModifiedDate = DateTime.Parse("10.07.2024 12:14:44"),
            CreatedBy = Guid.Parse("d3d8d19d-0da3-4499-8999-df423dea804a"),
            ModifiedBy = Guid.Parse("95c3ac7b-2e5a-4f77-9871-f70d9cf0c8a5")
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
            SomeValues = [111, 333],
            CreatingDate = DateTime.Parse("11.07.2024 12:14:44"),
            ModifiedDate = null,
            CreatedBy = Guid.Parse("317329de-8015-4615-968e-8bdb98687468"),
            ModifiedBy = null
        };

    #endregion GetProducts

    #region GetTestDataFiles

    private JToken Get_p1ToP2WithDifference() => GetFile();
    
    private JToken Get_p1ToP3WithDifference() => GetFile();
    
    private JToken Get_p2ToP1WithDifference() => GetFile();
    
    private JToken Get_p3ToP1WithDifference() => GetFile();
    
    private JToken Get_p2ToP3WithDifference() => GetFile();
    
    private JToken Get_p3ToP2WithDifference() => GetFile();
    
    private JToken Get_p1WithoutDifference() => GetFile();
    
    private JToken Get_p2WithoutDifference() => GetFile();
    
    private JToken Get_p3WithoutDifference() => GetFile();
    
    private JToken GetFile([CallerMemberName]string fileNameMethod = "")
    {
        var fileName = $"TestData/{fileNameMethod.Replace("Get_", "")}.json";
        
        if (!File.Exists(fileName)) 
            throw new Exception($"Файл {fileName} не найден.");
        
        var file = File.ReadAllText(fileName);
            
        return JToken.Parse(file);
    }

    #endregion
}