using Microsoft.Extensions.DependencyInjection;

namespace Rosettes.core
{
    public static class ServiceManager
    {
#pragma warning disable CS8618 // Un campo que no acepta valores NULL debe contener un valor distinto de NULL al salir del constructor. Considere la posibilidad de declararlo como que admite un valor NULL.
        public static IServiceProvider Provider { get; private set; }
#pragma warning restore CS8618 // Un campo que no acepta valores NULL debe contener un valor distinto de NULL al salir del constructor. Considere la posibilidad de declararlo como que admite un valor NULL.

        public static void SetProvider(ServiceCollection collection) => Provider = collection.BuildServiceProvider();

#pragma warning disable CS8714
        public static T GetService<T>() where T : new()
        {
            if (Provider == null) throw new ArgumentNullException(nameof(Provider));
            return Provider.GetRequiredService<T>();
        }
#pragma warning restore CS8714
    }
}
