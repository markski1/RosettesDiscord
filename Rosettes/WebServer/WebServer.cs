namespace Rosettes.WebServer;

public static class WebServer
{
    private static Thread? _webServerThread;
    private static WebApplication? _app;

    public static void Initialize(WebApplication app)
    {
        _app = app;
        _webServerThread = new(RunWebServer);
        _webServerThread.Start();
    }

    private static void RunWebServer()
    {
        _app?.Run();
    }
}
