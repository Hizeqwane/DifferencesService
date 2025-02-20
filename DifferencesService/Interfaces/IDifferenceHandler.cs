using DifferencesService.Models;

namespace DifferencesService.Interfaces;

public interface IDifferenceHandler
{
    object? Build(Type typeOfObject, IEnumerable<Difference> differences);
    
    void Patch(object sourceObject, IEnumerable<Difference> differences);
    
    void Unpatch(object sourceObject, IEnumerable<Difference> differences);

    IEnumerable<Difference> GetDifferences(object? primaryObj, object? secondaryObj);
    
    IEnumerable<Difference> GetRevertingDifferences(IEnumerable<Difference> differences, Type typeOfObject);
}