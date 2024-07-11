using System.Collections;
using DifferencesService.Models;

namespace DifferencesService.Interfaces;

public interface IDifferenceHandler
{
    object Build(Type typeOfObject, IEnumerable<Difference> differences);
    
    object Patch(object sourceObject, IEnumerable<Difference> differences);

    IEnumerable<Difference> GetDifferences(object? primaryObj, object secondaryObj);
}