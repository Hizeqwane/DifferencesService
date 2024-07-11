namespace DifferencesService.Test.Models;

public class Document
{
    public int Id { get; set; }
    
    public string Name { get; set; }
    
    public List<Attachment> Attachments { get; set; }
}