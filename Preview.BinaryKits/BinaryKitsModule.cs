using Core.Services;

using Prism.Ioc;
using Prism.Modularity;

namespace PreviewBinaryKits;

[Module(ModuleName = "BinaryKitsPreview", OnDemand = false)]
public class BinaryKitsModule : IModule
{
    public void OnInitialized(IContainerProvider containerProvider)
    {
        if (SettingsService.Instance.Preview == "BinaryKits")
        {
            PreviewServiceProvider.Set(content => new BinaryKits(content));
        }
    }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    { }
}
