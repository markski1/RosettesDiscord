namespace Rosettes.Managers;

public static class ServiceManager
{
    public static IServiceProvider Provider { get; private set; } = null!;

    public static void SetProvider(ServiceCollection collection) => Provider = collection.BuildServiceProvider();

    public static T GetService<T>() where T : notnull
    {
        return Provider.GetRequiredService<T>();
    }
}
