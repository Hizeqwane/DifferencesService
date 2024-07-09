using DifferencesService.Models;

namespace DifferencesService.Interfaces;

public interface IDifferenceHandler
{
    object Patch(object sourceObject, IEnumerable<Difference> differences);

    IEnumerable<Difference> GetDifferences(object primaryObj, object secondaryObj);
}