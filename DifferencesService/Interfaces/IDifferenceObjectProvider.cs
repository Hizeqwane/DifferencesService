using DifferencesService.Models;
using Newtonsoft.Json.Linq;

namespace DifferencesService.Interfaces;

public interface IDifferenceObjectProvider
{
    JToken? GetObjectWithDifferences(object source, IEnumerable<Difference> differences, bool getEmptyProperties = true);
}