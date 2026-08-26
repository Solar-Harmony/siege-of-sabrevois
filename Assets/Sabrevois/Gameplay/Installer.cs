using Piper;
using Sabrevois.Gameplay.AI.Actions;
using Zenject;

namespace Sabrevois.Gameplay
{
    public class GameplayInstaller : Installer<GameplayInstaller>
    {
        public override void InstallBindings()
        {
            Container.Bind<AttackService>().AsSingle();
            Container.Bind<PiperManager>().FromComponentInHierarchy().AsSingle();
        }
    }
}