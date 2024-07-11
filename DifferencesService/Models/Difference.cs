namespace DifferencesService.Models;

public class Difference
{
    public object Id { get; set; } = default!;

    public object EntityId { get; set; } = default!;

    public string? PropertyPath { get; set; }
        
    public object? OldValue { get; set; }
        
    public object? NewValue { get; set; }
    
    public List<Difference>? Childs { get; set; }
}