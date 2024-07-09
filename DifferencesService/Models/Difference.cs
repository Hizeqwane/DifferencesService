namespace DifferencesService.Models;

public class Difference
{
    public object Id { get; set; } = default!;

    public object EntityId { get; set; } = default!;

    public string? PropertyPath { get; set; }
        
    public string? OldValue { get; set; }
        
    public string? NewValue { get; set; }
    
    public List<Difference>? Childs { get; set; }
}