namespace DifferencesService.Test.Models;

public class Document<T> : Entity<T>
{
    public string Name { get; set; }

    public List<Attachment<T>> Attachments { get; set; }
}