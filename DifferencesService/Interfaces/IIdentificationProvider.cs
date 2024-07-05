namespace DifferencesService.Interfaces;

public interface IIdentificationProvider
{
    Type GetIdType();
    
    object GetNextObjectId();

    void Flush();
}