using System.Reflection;

namespace DifferencesService.Interfaces;

public interface IIdentificationService
{
    public object GetNextId();
    
    public void Flush();

    PropertyInfo FindIdPropertyAndThrow(Type typeOfObject, PropertyInfo[]? properties = null);

    string GetIdPropertyName(Type objType);
}