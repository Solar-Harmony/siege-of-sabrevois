using Piper;
using Sabrevois.Gameplay.AI.Actions;
using Sabrevois.Gameplay.Dialogue;
using Zenject;

namespace Sabrevois.Gameplay
{
    public class GameplayInstaller : Installer<GameplayInstaller>
    {
        public override void InstallBindings()
        {
            Container.Bind<ConversationService>().AsSingle();
            Container.Bind<AttackService>().AsSingle();
            Container.BindInterfacesTo<NPCBarkService>().AsSingle();
            Container.Bind<PiperManager>().FromComponentInHierarchy().AsSingle();
        }
    }
}