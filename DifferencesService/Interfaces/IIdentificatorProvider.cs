namespace DifferencesService.Interfaces;

public interface IIdentificatorProvider<TId>
{
    public TId GetNextId();
    
    public void Flush();
}