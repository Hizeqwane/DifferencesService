namespace DifferencesService.Test.Models;

public class Attachment<T> : Entity<T>
{
    public string FileName { get; set; }
}