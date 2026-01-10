using Sabrevois.AI.Actions;
using Sabrevois.Gameplay.AI.Actions;
using Sabrevois.Gameplay.Dialogue;
using VContainer;

namespace Sabrevois.Gameplay
{
    public static class Installer
    {
        public static void Configure(IContainerBuilder builder)
        {
            builder.Register<ConversationService>(Lifetime.Singleton);
        }
    }
}