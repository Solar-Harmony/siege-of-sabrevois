using VContainer;
using VContainer.Unity;

namespace Sabrevois
{
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            Gameplay.Installer.Configure(builder);
            AI.Installer.Configure(builder);
        }
    }
}