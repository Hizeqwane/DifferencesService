namespace DifferencesService.Test.Models;

public class Product<T> : Entity<T>
{
    public string Name { get; set; }

    public License<T> License { get; set; }

    public Registration<T> Registration { get; set; }

    public List<Document<T>> Documents { get; set; }
}