using DifferencesService.Interfaces;

namespace DifferencesService.Modules.IdentificationProviders;

public class GuidIdentificationProvider : IIdentificationProvider
{
    public Type GetIdType() => typeof(Guid);
    
    public object GetNextObjectId() => Guid.NewGuid();

    public void Flush() { }
}