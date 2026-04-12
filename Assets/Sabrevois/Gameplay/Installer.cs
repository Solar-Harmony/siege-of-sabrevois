using Sabrevois.Gameplay.Dialogue;
using Zenject;

namespace Sabrevois.Gameplay
{
    public class GameplayInstaller : Installer<GameplayInstaller>
    {
        public override void InstallBindings()
        {
            Container.Bind<ConversationService>().AsSingle();
            Container.Bind<FuckYouService>().AsSingle();
        }
    }
}