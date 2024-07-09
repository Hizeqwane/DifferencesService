namespace DifferencesService.Test.Models;

public class Product
{
    public int Id { get; set; }
    
    public int[] SomeValues { get; set; }
    
    public string Name { get; set; }

    public License License { get; set; }

    public List<Document> Documents { get; set; }
}