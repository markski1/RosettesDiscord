using Discord.Interactions;
using Discord.WebSocket;
using Rosettes.Modules.Commands;
using Rosettes.Modules.Engine;
using System.Reflection;

namespace Rosettes.Core
{
    public class InteractionManager
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _commands;
        private readonly IServiceProvider _services;

        public InteractionManager(DiscordSocketClient client, InteractionService commands, IServiceProvider services)
        {
            _client = client;
            _commands = commands;
            _services = services;
        }

        private async Task OnInteraction(SocketInteraction inter)
        {
            try
            {
                // get interaction context
                var context = new SocketInteractionContext(_client, inter);
                await _commands.ExecuteCommandAsync(context, _services);
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("InteractionManager", $"{ex}");

                // acknoweldge we crashed.
                await inter.RespondAsync("Sorry, there was an unknown error executing the command.", ephemeral: true);
            }
        }

        private async Task OnButtonClicked(SocketMessageComponent component)
        {
            switch (component.Data.CustomId)
            {
                case "fish":
                    await RpgEngine.CatchFishFunc(component, component.User);
                    break;
                case "inventory":
                    await RpgEngine.ShowInventoryFunc(component, component.User);
                    break;
                case "shop":
                    await RpgEngine.ShowShopFunc(component, component.User);
                    break;
                default:
                    await component.RespondAsync(await PollEngine.VoteInPoll(component.User.Id, component.Message, component.Data.CustomId), ephemeral: true);
                    break;
            }
        }

        private async Task OnMenuSelectionMade(SocketMessageComponent component)
        {
            if (component.Data.CustomId is "buy" or "sell") {
                await RpgEngine.ShopAction(component);
            }
            if (component.Data.CustomId is "make")
            {
                await RpgEngine.CraftAction(component);
            }
        }

        private Task OnCommandExecuted(SlashCommandInfo arg1, Discord.IInteractionContext arg2, IResult arg3)
        {
            return Task.CompletedTask;
        }

        public async Task SetupAsync()
        {
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            await _commands.RegisterCommandsGloballyAsync();

            _client.InteractionCreated += OnInteraction;

            _client.ButtonExecuted += OnButtonClicked;

            _client.SelectMenuExecuted += OnMenuSelectionMade;

            _commands.SlashCommandExecuted += OnCommandExecuted;
        }
    }
}
