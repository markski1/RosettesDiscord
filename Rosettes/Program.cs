using Rosettes.Core;
using Rosettes.WebServer;

WebServer.Initialize(args);

// Initialize bot
await Global.RosettesMain.MainAsync();