using DifferencesService.Interfaces;

namespace DifferencesService.Modules;

public class IntIdentificationProvider : IIdentificationProvider
{
    private int _intId = 0;

    public Type GetIdType() => typeof(int);
    
    public object GetNextObjectId()
    {
        Interlocked.Increment(ref _intId);

        return _intId;
    }

    public void Flush() => _intId = 0;
}