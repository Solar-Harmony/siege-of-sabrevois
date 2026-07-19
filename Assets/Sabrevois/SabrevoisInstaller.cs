using Sabrevois.AI;
using SolarHarmony.DynamicWounds2D;
using Sabrevois.Gameplay;
using Zenject;

namespace Sabrevois
{
    public class SabrevoisInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            AIInstaller.Install(Container);
            GameplayInstaller.Install(Container);
            DynamicWounds2DInstaller.Install(Container);
            
            Container.BindInterfacesTo<Sabrevois.UI.DamageNumberSpawner>().AsSingle();
            Container.BindMemoryPool<Sabrevois.UI.DamageNumber, Sabrevois.UI.DamageNumber.Pool>()
                .WithInitialSize(10)
                .FromNewComponentOnNewGameObject()
                .UnderTransformGroup("DamageNumbers");

            Container.BindInterfacesTo<Sabrevois.UI.MissTextSpawner>().AsSingle();
            Container.BindMemoryPool<Sabrevois.UI.DamageNumber, Sabrevois.UI.DamageNumber.MissTextPool>()
                .WithInitialSize(5)
                .FromNewComponentOnNewGameObject()
                .UnderTransformGroup("DamageNumbers");
        }
    }
}