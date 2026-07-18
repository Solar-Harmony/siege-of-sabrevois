using Zenject;

namespace SolarHarmony.DynamicWounds2D
{
    public class DynamicWounds2DInstaller : Installer<DynamicWounds2DInstaller>
    {
        public override void InstallBindings()
        {
            Container.Bind<GlobalWoundManager>().FromComponentInHierarchy().AsSingle();
        }
    }
}
