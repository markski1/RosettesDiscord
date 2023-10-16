namespace Rosettes.Managers;

public static class ServiceManager
{
#pragma warning disable CS8618
    public static IServiceProvider Provider { get; private set; }
#pragma warning restore CS8618

    public static void SetProvider(ServiceCollection collection) => Provider = collection.BuildServiceProvider();

#pragma warning disable CS8714
    public static T GetService<T>() where T : new()
    {
        if (Provider == null) throw new ArgumentNullException(nameof(Provider));
        return Provider.GetRequiredService<T>();
    }
#pragma warning restore CS8714
}
