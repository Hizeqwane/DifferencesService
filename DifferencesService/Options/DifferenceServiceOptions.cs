using DifferencesService.Interfaces;
using DifferencesService.Modules;

namespace DifferencesService.Options;

public class DifferenceServiceOptions
{
    public string DefaultIdPropertyName { get; set; } = "Id";
    public Dictionary<Type, string> IdentificationIdPropertyNameMap { get; } = new();
    public Dictionary<Type, IIdentificationProvider> IdentificationProvidersMap { get; } = new();

    private static IIdentificationProvider DefaultIdentificationProvider { get; } = new IntIdentificationProvider();

    public void SetDefaultIdentificationProvider() =>
        IdentificationProvidersMap.Add(DefaultIdentificationProvider.GetIdType(), DefaultIdentificationProvider);
}