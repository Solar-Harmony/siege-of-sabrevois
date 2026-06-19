using Sabrevois.AI;
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
            
            Container.BindInterfacesTo<Sabrevois.UI.DamageNumberSpawner>().AsSingle();
            Container.BindMemoryPool<Sabrevois.UI.DamageNumber, Sabrevois.UI.DamageNumber.Pool>()
                .WithInitialSize(10)
                .FromNewComponentOnNewGameObject()
                .UnderTransformGroup("DamageNumbers");
        }
    }
}