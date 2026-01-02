using Sabrevois.AI;
using Sabrevois.AI.Actions;
using Sabrevois.Gameplay.AI.Actions;
using Sabrevois.Gameplay.Dialogue;
using VContainer;
using VContainer.Unity;

namespace Sabrevois.Gameplay
{
    public static class Installer
    {
        public static void Configure(IContainerBuilder builder)
        {
            builder.Register<ConversationService>(Lifetime.Singleton);

            builder.Register<ConverseAction>(Lifetime.Singleton);
            builder.Register<WhineAction>(Lifetime.Singleton);
        }
    }
}