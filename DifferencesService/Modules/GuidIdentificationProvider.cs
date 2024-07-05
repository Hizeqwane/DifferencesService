using DifferencesService.Interfaces;

namespace DifferencesService.Modules;

public class GuidIdentificationProvider : IIdentificationProvider
{
    public Type GetIdType() => typeof(Guid);
    
    public object GetNextObjectId() => Guid.NewGuid();

    public void Flush() { }
}