namespace DifferencesService.Test.Models;

public class License<T> : Entity<T>
{
    public string Name { get; set; }
    public string Type { get; set; }
}