using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Modules.Commands;
using Rosettes.Modules.Engine;
using Rosettes.Modules.Engine.Guild;
using Rosettes.Modules.Engine.Minigame;
using System.Reflection;

namespace Rosettes.Managers;

public class InteractionManager(DiscordSocketClient client, InteractionService commands, IServiceProvider services)
{
    private readonly DiscordSocketClient _client = client;
    private readonly InteractionService _commands = commands;
    private readonly IServiceProvider _services = services;

    private Task OnInteraction(SocketInteraction inter)
    {
        TelemetryEngine.Count(TelemetryType.Interaction);
        _ = Task.Run(async () =>
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
        });
        return Task.CompletedTask;
    }

    private Task OnButtonClicked(SocketMessageComponent component)
    {
        TelemetryEngine.Count(TelemetryType.Interaction);
        _ = Task.Run(async () =>
        {
            string action = component.Data.CustomId;

            if (action.Contains("null_")) return;

            // settings stuff
            if (action.Contains("toggle_"))
            {
                await AdminHelper.ChangeSettings(component);
                return;
            }

            // petting stuff
            if (action.Contains("doPet_"))
            {
                await PetEngine.PetAPet(component);
                return;
            }

            switch (action)
            {
                // farm stuff
                case "fish":
                    await FarmEngine.CatchFishFunc(component, component.User);
                    break;
                case "inventory":
                    await FarmEngine.ShowInventoryFunc(component, component.User);
                    break;
                case "shop":
                    await FarmEngine.ShowShopFunc(component, component.User);
                    break;
                case "pets":
                    await PetEngine.ShowPets(component, component.User);
                    break;
                case "farm":
                    await Farm.ShowFarm(component, component.User);
                    break;

                case "crops_plant":
                    await Farm.PlantSeed(component, component.User);
                    break;
                case "crops_water":
                    await Farm.WaterCrops(component, component.User);
                    break;
                case "crops_harvest":
                    await Farm.HarvestCrops(component, component.User);
                    break;

                case "pet_view":
                    await PetEngine.ViewPet(component, component.User);
                    break;
                case "pet_namechange":
                    PetEngine.BeginNameChange(component);
                    break;

                // if nothing else, it's poll stuff
                default:
                    await component.RespondAsync(await PollEngine.VoteInPoll(component.User.Id, component.Message, component.Data.CustomId), ephemeral: true);
                    break;
            }
        });
        return Task.CompletedTask;
    }

    private Task OnModalSubmitted(SocketModal modal)
    {
        _ = Task.Run(async () =>
        {
            List<SocketMessageComponentData> components = modal.Data.Components.ToList();
            switch (modal.Data.CustomId)
            {
                case "pollMaker":
                {
                    string question = components.First(x => x.CustomId == "question").Value;
                    string option1 = components.First(x => x.CustomId == "option1").Value;
                    string option2 = components.First(x => x.CustomId == "option2").Value;
                    string option3 = components.First(x => x.CustomId == "option3").Value;
                    string option4 = components.First(x => x.CustomId == "option4").Value;

                    await AdminCommands.FollowUpPoll(question, option1, option2, option3, option4, modal);
                    return;
                }
                case "petNamechange":
                {
                    string newName = components.First(x => x.CustomId == "newName").Value;

                    PetEngine.SetPetName(modal, newName);
                    break;
                }
            }
        });
        return Task.CompletedTask;
    }

    private Task OnMenuSelectionMade(SocketMessageComponent component)
    {
        _ = Task.Run(async () =>
        {
            if (component.Data.CustomId is "buy" or "sell" or "sell_e")
            {
                await FarmEngine.ShopAction(component);
            }
            if (component.Data.CustomId is "defaultPet")
            {
                await PetEngine.SetDefaultPet(component);
            }
            if (component.Data.CustomId.Contains("petFeed_"))
            {
                await PetEngine.FeedAPet(component);
            }
        });
        return Task.CompletedTask;
    }

    private Task OnGlobalCommandExecuted(SlashCommandInfo arg1, IInteractionContext arg2, Discord.Interactions.IResult arg3)
    {
        TelemetryEngine.Count(TelemetryType.Command);
        TelemetryEngine.CountCommand(arg1.Name);
        return Task.CompletedTask;
    }

    private Task OnGuildCommandExecuted(SocketSlashCommand arg)
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

        _client.ModalSubmitted += OnModalSubmitted;

        _client.SlashCommandExecuted += OnGuildCommandExecuted;

        _commands.SlashCommandExecuted += OnGlobalCommandExecuted;
    }
}
