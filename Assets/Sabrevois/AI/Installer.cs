using VContainer;
using VContainer.Unity;

namespace Sabrevois.AI
{
    public static class Installer
    {
        public static void Configure(IContainerBuilder builder)
        {
            builder.Register<DecisionMakingService>(Lifetime.Singleton);
        }
    }
}