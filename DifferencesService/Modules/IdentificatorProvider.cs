using DifferencesService.Interfaces;

namespace DifferencesService.Modules;

public class IdentificatorProvider<TId> : IIdentificatorProvider<TId>
{
    private int _intId = 0;

    private Type? _tIdType;

    private Type GetIdType()
    {
        if (_tIdType != null)
            return _tIdType;

        return _tIdType = typeof(TId);
    }

    public TId GetNextId()
    {
        if (GetIdType() == typeof(int))
        {
            Interlocked.Increment(ref _intId);
            return  _intId is TId intId
                ? intId
                : default!;
        }
        
        if (GetIdType() == typeof(Guid))
            return Guid.NewGuid() is TId guidId
                ? guidId
                : default!;

        throw new NotImplementedException($"Провайдер идентификаторов для типа {GetIdType().FullName} не реализован.");
    }

    public void Flush()
    {
        if (GetIdType() == typeof(int))
            _intId = 0;
    }
}