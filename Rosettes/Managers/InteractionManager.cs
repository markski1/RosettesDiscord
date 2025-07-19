using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Modules.Commands;
using Rosettes.Modules.Engine;
using System.Reflection;
using Rosettes.Modules.Commands.Utility;
using Rosettes.Modules.Minigame.Farming;
using Rosettes.Modules.Minigame.Pets;

namespace Rosettes.Managers;

public class InteractionManager(DiscordSocketClient client, InteractionService commands, IServiceProvider services)
{
    private Task OnInteraction(SocketInteraction inter)
    {
        TelemetryEngine.Count(TelemetryType.Interaction);
        _ = Task.Run(async () =>
        {
            try
            {
                // get interaction context
                var context = new SocketInteractionContext(client, inter);
                await commands.ExecuteCommandAsync(context, services);
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

            // pet stuff
            if (action.Contains("doPet_"))
            {
                await PetEngine.PetAPet(component);
                return;
            }
            
            // auth stuff
            if (action.Contains("auth_"))
            {
                await AuthEngine.HandleAuthInteraction(component);
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

                    await PetEngine.SetPetName(modal, newName);
                    break;
                }
                case "reminderMaker":
                {
                    var success = int.TryParse(components.First(x => x.CustomId == "time").Value, out var time);

                    if (!success)
                    {
                        await modal.RespondAsync("The 'time' amount entered is not a number.", ephemeral: true);
                        return;
                    }
                    
                    string unit =  components.First(x => x.CustomId == "unit").Value;
                    string message =  components.First(x => x.CustomId == "message").Value;

                    MiscCommands.FollowUpReminder(time, unit, message, modal);
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
            switch (component.Data.CustomId)
            {
                case "buy" or "sell" or "sell_e":
                    await FarmEngine.ShopAction(component);
                    break;
                case "defaultPet":
                    await PetEngine.SetDefaultPet(component);
                    break;
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
        await commands.AddModulesAsync(Assembly.GetEntryAssembly(), services);
        await commands.RegisterCommandsGloballyAsync();

        client.InteractionCreated += OnInteraction;

        client.ButtonExecuted += OnButtonClicked;

        client.SelectMenuExecuted += OnMenuSelectionMade;

        client.ModalSubmitted += OnModalSubmitted;

        client.SlashCommandExecuted += OnGuildCommandExecuted;

        commands.SlashCommandExecuted += OnGlobalCommandExecuted;
    }
}
