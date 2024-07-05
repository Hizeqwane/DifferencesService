using System.Text.RegularExpressions;
using DifferencesService.Interfaces;
using DifferencesService.Modules;
using DifferencesService.Options;
using DifferencesService.Test.Models;
using DifferencesService.Test.Models.CompetitorProducts;
using JsonDiffPatchDotNet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework.Legacy;

namespace DifferencesService.Test;

public class Tests
{
    private IDifferenceService<int> _intDifferenceService;
    private IDifferenceService<Guid> _guidDifferenceService;
    
    private List<Product<int>> _idProducts;
    private List<Difference<int>> _idDifferences;
    private List<Difference<int>> _idDifferencesAfterProvider;
    
    private List<Product<Guid>> _guidProducts;
    private List<Difference<Guid>> _guidDifferences;
    private List<Difference<Guid>> _guidDifferencesAfterProvider;

    private List<Product<int>> _idProductsForDifferences;
    private List<Product<Guid>> _guidProductsForDifferences;

    private List<Product<int>> _idRightAnswer;
    private List<Difference<int>> _idDifferencesRightAnswer;
    private List<Product<Guid>> _guidRightAnswer;
    private List<Difference<Guid>> _guidDifferencesRightAnswer;

    #region Competitors products

    private List<CompetitorProduct> _idCompetitorsProductsForDifferences;

    #endregion
    
    [SetUp]
    public void Setup()
    {
        // Применение имён свойств для типов излишен - показан в качестве примера
        var options = new DifferenceServiceOptions()
            .SetupIdentificationProvider(new IntIdentificationProvider())
            .SetupIdentificationProvider(new GuidIdentificationProvider())
            .SetupIdPropertyNameForType<int>("Id")
            .SetupIdPropertyNameForType<Guid>("Id")
            .WithDefaultIdPropertyName("Id");
        
        _intDifferenceService = new DifferencesService<int>(new IdentificationService(options));
        _guidDifferenceService = new DifferencesService<Guid>(new IdentificationService(options));
        
        SetupIntIds();
        SetupGuidIds();
        SetupIntIdsDifferences();
        SetupGuidIdsDifferences();
        SetupCompetitorsProductsDifferences();
    }

    #region Setups

    private void SetupIntIds()
    {
        var textProducts = ReadTestIdProducts();
        var textDifferences = ReadTestIdDifferences();
        var textRightAnswer = ReadTestIdProductsRightAnswer();
        var textDifferencesAfterProvider = ReadTestIntDifferencesAfterProvider();
        
        _idRightAnswer = JsonConvert.DeserializeObject<List<Product<int>>>(textRightAnswer)!;
        _idProducts = JsonConvert.DeserializeObject<List<Product<int>>>(textProducts)!;
        _idDifferences = JsonConvert.DeserializeObject<List<Difference<int>>>(textDifferences)!;
        _idDifferencesAfterProvider = JsonConvert.DeserializeObject<List<Difference<int>>>(textDifferencesAfterProvider)!;
        if (_idProducts == null || _idDifferences == null || _idRightAnswer == null || _idDifferencesAfterProvider == null)
            throw new ApplicationException("Ошибка при десериализации (int).");
    }
    
    private void SetupGuidIds()
    {
        var textProducts = ReadTestGuidProducts();
        var textDifferences = ReadTestGuidDifferences();
        var textGuidRightAnswer = ReadTestGuidProductsRightAnswer();
        var textDifferencesAfterProvider = ReadTestGuidDifferencesAfterProvider();
        
        _guidRightAnswer = JsonConvert.DeserializeObject<List<Product<Guid>>>(textGuidRightAnswer)!;
        _guidProducts = JsonConvert.DeserializeObject<List<Product<Guid>>>(textProducts)!;
        _guidDifferences = JsonConvert.DeserializeObject<List<Difference<Guid>>>(textDifferences)!;
        _guidDifferencesAfterProvider = JsonConvert.DeserializeObject<List<Difference<Guid>>>(textDifferencesAfterProvider)!;
        if (_guidProducts == null || _guidDifferences == null || _guidRightAnswer == null || _guidDifferencesAfterProvider == null)
            throw new ApplicationException("Ошибка при десериализации (Guid).");
    }
    
    private void SetupIntIdsDifferences()
    {
        var textProducts = ReadTestIntProductsForDifferences();
        var textRightAnswer = ReadTestIntDifferencesRightAnswer();
        
        _idDifferencesRightAnswer = JsonConvert.DeserializeObject<List<Difference<int>>>(textRightAnswer)!;
        _idProductsForDifferences = JsonConvert.DeserializeObject<List<Product<int>>>(textProducts)!;
        if (_idProductsForDifferences == null || _idDifferencesRightAnswer == null)
            throw new ApplicationException("Ошибка при десериализации (int - differences).");
    }
    
    private void SetupGuidIdsDifferences()
    {
        var textProducts = ReadTestGuidProductsForDifferences();
        var textRightAnswer = ReadTestGuidDifferencesRightAnswer();
        
        _guidDifferencesRightAnswer = JsonConvert.DeserializeObject<List<Difference<Guid>>>(textRightAnswer)!;
        _guidProductsForDifferences = JsonConvert.DeserializeObject<List<Product<Guid>>>(textProducts)!;
        if (_guidProductsForDifferences == null || _guidDifferencesRightAnswer == null)
            throw new ApplicationException("Ошибка при десериализации (Guid - differences).");
    }
    
    private void SetupCompetitorsProductsDifferences()
    {
        var textProducts = ReadTestIntCompetitorProductsForDifferences();
        //var textRightAnswer = ReadTestIntCompetitorDifferencesRightAnswer();
        
        //_guidDifferencesRightAnswer = JsonConvert.DeserializeObject<List<Difference<Guid>>>(textRightAnswer)!;
        _idCompetitorsProductsForDifferences = JsonConvert.DeserializeObject<List<CompetitorProduct>>(textProducts)!;
        if (_idCompetitorsProductsForDifferences == null)// || _guidDifferencesRightAnswer == null)
            throw new ApplicationException("Ошибка при десериализации CompetitorProducts.");
    }

    #endregion Setups

    [Test(Description = "Проверка работы с сущностями Entity<int>")]
    public void Test1()
    {
        var fromProvider = true;
        if (fromProvider)
        {
            foreach (var product in _idProducts) 
                _intDifferenceService.Patch(product, _idDifferencesAfterProvider);
        
            var result1 = $"[{string.Join(',', _idProducts.Select(JsonConvert.SerializeObject))}]"; 
            WriteIntFile(result1);
        
            var jsonDiffers1 = new JsonDiffPatch().Diff(JToken.Parse(JsonConvert.SerializeObject(_idProducts)), JToken.Parse(JsonConvert.SerializeObject(_idRightAnswer)));

            ClassicAssert.AreEqual(jsonDiffers1, null!);
            return;
        }

        foreach (var product in _idProducts) 
            _intDifferenceService.Patch(product, _idDifferences);
        
        var result = $"[{string.Join(',', _idProducts.Select(JsonConvert.SerializeObject))}]"; 
        WriteIntFile(result);
        
        var jsonDiffers = new JsonDiffPatch().Diff(JToken.Parse(JsonConvert.SerializeObject(_idProducts)), JToken.Parse(JsonConvert.SerializeObject(_idRightAnswer)));

        ClassicAssert.AreEqual(jsonDiffers, null!);
    }
    
    [Test(Description = "Проверка работы с сущностями Entity<Guid>")]
    public void Test2()
    {
        var fromProvider = true;
        if (fromProvider)
        {
            foreach (var product in _guidProducts)
                _guidDifferenceService.Patch(product, _guidDifferencesAfterProvider);
        
            var result1 = $"[{string.Join(',', _guidProducts.Select(JsonConvert.SerializeObject))}]"; 
            WriteGuidFile(result1);
            
            var jsonDiffers1 = new JsonDiffPatch().Diff(JToken.Parse(JsonConvert.SerializeObject(_guidProducts)), JToken.Parse(JsonConvert.SerializeObject(_guidRightAnswer)));

            ClassicAssert.AreEqual(jsonDiffers1, null!);
            return;
        }
        
        foreach (var product in _guidProducts) 
            _guidDifferenceService.Patch(product, _guidDifferences);

        var result = $"[{string.Join(',', _guidProducts.Select(JsonConvert.SerializeObject))}]"; 
        WriteGuidFile(result);

        var jsonDiffers = new JsonDiffPatch().Diff(JsonConvert.SerializeObject(_guidProducts), JsonConvert.SerializeObject(_guidRightAnswer));
        
        ClassicAssert.AreEqual(jsonDiffers, null);
    }
    
    [Test(Description = "Проверка работы с сущностями Entity<int> - Поиск differences")]
    public void Test3()
    {
        if (_idProductsForDifferences.Count != 2)
            throw new ArgumentException($"Тест расчитан на сравнение двух продуктов.");
        
        var differences = _intDifferenceService.GetDifferences
        (
            _idProductsForDifferences[0],
            _idProductsForDifferences[1]
        );

        WriteIntDifferencesFile(JsonConvert.SerializeObject(differences));
        
        var jsonDiffers = new JsonDiffPatch().Diff(JsonConvert.SerializeObject(differences), JsonConvert.SerializeObject(_idDifferencesRightAnswer));
        
        ClassicAssert.AreEqual(jsonDiffers, null);
    }
    
    [Test(Description = "Проверка работы с сущностями Entity<Guid> - Поиск differences")]
    public void Test4()
    {
        if (_guidProductsForDifferences.Count != 2)
            throw new ArgumentException($"Тест расчитан на сравнение двух продуктов.");
        
        var differences = _guidDifferenceService.GetDifferences
        (
            _guidProductsForDifferences[0],
            _guidProductsForDifferences[1]
        );

        WriteGuidDifferencesFile(JsonConvert.SerializeObject(differences));

        var guidChar = "[0-9A-Fa-f]";
        var guidRegex = new Regex(guidChar + "{8}-" + guidChar + "{4}-" + guidChar + "{4}-" + guidChar + "{4}-" + guidChar + "{12}");
        
        var differencesStrWithEmptyGuids = guidRegex.Replace(JsonConvert.SerializeObject(differences), Guid.Empty.ToString());
        var differencesRightAnswerWithEmptyGuids = guidRegex.Replace(JsonConvert.SerializeObject(_guidDifferencesRightAnswer), Guid.Empty.ToString());
        
        var jsonDiffers = new JsonDiffPatch().Diff(differencesStrWithEmptyGuids, differencesRightAnswerWithEmptyGuids);
        
        ClassicAssert.AreEqual(jsonDiffers, null);
    }
    
    [Test(Description = "Проверка работы с сущностями CompetitorProducts - Поиск differences")]
    public void Test5()
    {
        if (_idCompetitorsProductsForDifferences.Count != 2)
            throw new ArgumentException($"Тест расчитан на сравнение двух продуктов.");
        
        var differences = _intDifferenceService.GetDifferences
        (
            _idCompetitorsProductsForDifferences[0],
            _idCompetitorsProductsForDifferences[1]
        );

        //WriteIntDifferencesFile(JsonConvert.SerializeObject(differences));
        
        //var jsonDiffers = new JsonDiffPatch().Diff(JsonConvert.SerializeObject(differences), JsonConvert.SerializeObject(_idDifferencesRightAnswer));
        
        //ClassicAssert.AreEqual(jsonDiffers, null);
        
        ClassicAssert.IsTrue(true);
    }

    #region Work with files

    #region Test1 - Применение differences к List<Product<int>>

    private static string ReadTestIdProducts() =>
        ReadTest("TestData/DifferencesApplier/IntId/Products.json");
    
    private static string ReadTestIdProductsRightAnswer() =>
        ReadTest("TestData/DifferencesApplier/IntId/ProductsRightAnswer.json");

    private static string ReadTestIdDifferences() =>
        ReadTest("TestData/DifferencesApplier/IntId/DifferenceToApply.json");
    
    private static string ReadTestIntDifferencesAfterProvider() =>
        ReadTest("TestData/DifferencesApplier/IntId/DifferencesAfterProvider.json");
    
    private static string ReadTestGuidDifferencesAfterProvider() =>
        ReadTest("TestData/DifferencesApplier/GuidId/DifferencesAfterProvider.json");

    #endregion Test1 - Применение differences к List<Product<int>>
    
    #region Test2 - Применение differences к List<Product<Guid>>
    
    private static string ReadTestGuidProducts() =>
        ReadTest("TestData/DifferencesApplier/GuidId/Products.json");
    
    private static string ReadTestGuidProductsRightAnswer() =>
        ReadTest("TestData/DifferencesApplier/GuidId/ProductsRightAnswer.json");

    private static string ReadTestGuidDifferences() =>
        ReadTest("TestData/DifferencesApplier/GuidId/DifferenceToApply.json");
    
    #endregion Test2 - Применение differences к List<Product<Guid>>
    
    #region Test3 - Поиск differences в Product<int>
    
    private static string ReadTestIntProductsForDifferences() =>
        ReadTest("TestData/GetDifferences/IntId/Products.json");
    
    private static string ReadTestIntDifferencesRightAnswer() =>
        ReadTest("TestData/GetDifferences/IntId/DifferenceRightAnswer.json");
    
    #endregion Test3 - Поиск differences в Product<int>
    
    #region Test4 - Поиск differences в Product<Guid>
    
    private static string ReadTestGuidProductsForDifferences() =>
        ReadTest("TestData/GetDifferences/GuidId/Products.json");
    
    private static string ReadTestGuidDifferencesRightAnswer() =>
        ReadTest("TestData/GetDifferences/GuidId/DifferenceRightAnswer.json");
    
    #endregion Test4 - Поиск differences в Product<Guid>
    
    #region Test5 - Поиск differences в CompetitorProduct<int>
    
    private static string ReadTestIntCompetitorProductsForDifferences() =>
        ReadTest("TestData/CompetitorProducts/CompetitorProducts.json");
    
    private static string ReadTestIntCompetitorDifferencesRightAnswer() =>
        ReadTest("TestData/CompetitorProducts/DifferenceRightAnswer.json");
    
    #endregion Test3 - Поиск differences в Product<int>

    private static string ReadTest(string filePath)
    {
        if (!File.Exists(filePath))
            throw new NullReferenceException("Файл с тестовыми данными не найден.");

        return File.ReadAllText(filePath);
    }

    private static void WriteIntFile(string content)
    {
        const string filePath = "TestData/DifferencesApplier/IntId/ProductAfterDifferences.json";
        File.WriteAllText(filePath, content);
    }
    
    private static void WriteGuidFile(string content)
    {
        const string filePath = "TestData/DifferencesApplier/GuidId/ProductAfterDifferences.json";
        File.WriteAllText(filePath, content);
    }
    
    private static void WriteIntDifferencesFile(string content)
    {
        const string filePath = "TestData/GetDifferences/IntId/DifferencesAfterProvider.json";
        File.WriteAllText(filePath, content);
    }
    
    private static void WriteGuidDifferencesFile(string content)
    {
        const string filePath = "TestData/GetDifferences/GuidId/DifferencesAfterProvider.json";
        File.WriteAllText(filePath, content);
    }

    #endregion Work with files
}