using DifferencesService.Modules;

namespace DifferencesService;

public class DifferenceEntityIdDistinctor<TId> : IEqualityComparer<Difference<TId>>
{
#pragma warning disable CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
    public bool Equals(Difference<TId> x, Difference<TId> y)
#pragma warning restore CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;
        
        return x.EntityId != null &&
               x.EntityId.IsEqualsFromToString(y.EntityId);
    }

    public int GetHashCode(Difference<TId> obj) => obj?.EntityId?.ToString()?.GetHashCode() ?? default;
}