namespace Rosettes.WebServer;

public static class WebServer
{
    private static Thread? webServerThread;
    private static WebApplication? app;

    public static void Initialize(WebApplication _app)
    {
        app = _app;
        webServerThread = new(RunWebServer);
        webServerThread.Start();
    }

    public static void RunWebServer()
    {
        app?.Run();
    }
}
