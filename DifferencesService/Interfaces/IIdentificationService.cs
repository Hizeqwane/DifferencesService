using System.Reflection;

namespace DifferencesService.Interfaces;

public interface IIdentificationService
{
    public TId GetNextId<TId>();
    
    public void Flush<TId>();

    PropertyInfo FindIdPropertyAndThrow(Type typeOfObject, PropertyInfo[]? properties = null);

    string GetIdPropertyName(Type objType);
}