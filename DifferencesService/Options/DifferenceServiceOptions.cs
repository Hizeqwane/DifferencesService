using DifferencesService.Interfaces;
using DifferencesService.Modules;
using DifferencesService.Modules.IdentificationProviders;

namespace DifferencesService.Options;

public class DifferenceServiceOptions
{
    public string DefaultIdPropertyName { get; set; } = "Id";
    public Dictionary<Type, string> IdentificationIdPropertyNameMap { get; } = new();
    public IIdentificationProvider? IdentificationProvider { get; private set; }

    public static IIdentificationProvider DefaultIdentificationProvider { get; } = new IntIdentificationProvider();

    public void SetIdentificationProvider(IIdentificationProvider provider) =>
        IdentificationProvider = provider;
    
    public void SetDefaultIdentificationProvider() =>
        IdentificationProvider = DefaultIdentificationProvider;

    public bool GetEmptyProperties { get; private set; } = true;

    public void SetEmptyPropertiesBehaviour(bool value) => GetEmptyProperties = value;
}