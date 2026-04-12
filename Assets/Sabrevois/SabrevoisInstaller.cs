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
        }
    }
}