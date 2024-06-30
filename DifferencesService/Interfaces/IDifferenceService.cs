namespace DifferencesService.Interfaces;

public interface IDifferenceService<TId>
{
    void Patch(object sourceObject, IEnumerable<Difference<TId>> differences);

    IEnumerable<Difference<TId>> GetDifferences(object primaryObj, object secondaryObj);
}