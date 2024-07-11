using Newtonsoft.Json.Linq;

namespace DifferencesService.Models;

public class DifferenceValue
{
    public object? Value { get; set; }
    
    public object? OldValue { get; set; }

    public string Type { get; set; } = DifferenceType.None;

    public JToken GetToken() => JToken.FromObject(this);
}