namespace DifferencesService;

public class Difference<T>
{
    public T Id { get; set; } = default!;

    public T EntityId { get; set; } = default!;

    public string? PropertyPath { get; set; }
        
    public string? OldValue { get; set; }
        
    public string? NewValue { get; set; }
    
    public List<Difference<T>>? Childs { get; set; }
}