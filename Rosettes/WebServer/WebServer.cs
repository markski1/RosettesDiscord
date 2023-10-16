namespace Rosettes.WebServer;

public static class WebServer
{
    private static string[] _args = Array.Empty<string>();

    private static Thread? webServerThread;

    public static void Initialize(string[] args)
    {
        _args = args;
        webServerThread = new(RunWebServer);
        webServerThread.Start();
    }

    public static void RunWebServer()
    {
        var builder = WebApplication.CreateBuilder(_args);

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapControllers();

        app.UseCors(x => x
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .SetIsOriginAllowed(origin => true)
                    .AllowCredentials());

        app.Run();
    }
}
