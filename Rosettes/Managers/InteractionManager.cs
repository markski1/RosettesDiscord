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
        if (inter is not SocketSlashCommand and
            not SocketMessageCommand and
            not SocketUserCommand)
        {
            return Task.CompletedTask;
        }

        TelemetryEngine.Count(TelemetryType.Interaction);
        _ = Task.Run(async () =>
        {
            try
            {
                // get interaction context
                var context = new SocketInteractionContext(client, inter);
                var result = await commands.ExecuteCommandAsync(context, services);
                if (!result.IsSuccess)
                {
                    Global.GenerateErrorMessage("InteractionManager", result.ErrorReason);
                    await SendInteractionFailure(inter);
                }
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("InteractionManager", $"{ex}");
                await SendInteractionFailure(inter);
            }
        });
        return Task.CompletedTask;
    }

    private static async Task SendInteractionFailure(SocketInteraction interaction)
    {
        const string message = "Sorry, there was an unknown error executing the command.";

        try
        {
            if (interaction.HasResponded)
                await interaction.FollowupAsync(message, ephemeral: true);
            else
                await interaction.RespondAsync(message, ephemeral: true);
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("InteractionManager response", $"{ex}");
        }
    }

    private static Task RunInteractionHandler(SocketInteraction interaction, Func<Task> handler)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await handler();
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("InteractionManager", $"{ex}");
                await SendInteractionFailure(interaction);
            }
        });
        return Task.CompletedTask;
    }

    private static bool IsFarmComponent(string action) =>
        action is "fish" or "inventory" or "shop" or "farm" or
            "crops_plant" or "crops_water" or "crops_harvest" or "plots_repair";

    private Task OnButtonClicked(SocketMessageComponent component)
    {
        TelemetryEngine.Count(TelemetryType.Interaction);
        return RunInteractionHandler(component, async () =>
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
                await FarmEngine.RunUserActionAsync(component, () => PetEngine.PetAPet(component));
                return;
            }
            
            // auth stuff
            if (action.Contains("auth_"))
            {
                await AuthEngine.HandleAuthInteraction(component);
                return;
            }

            if (IsFarmComponent(action))
            {
                string isAllowed = await FarmEngine.CanUseFarmComponent(component);
                if (isAllowed != "yes")
                {
                    await component.RespondAsync(isAllowed, ephemeral: true);
                    return;
                }
            }

            switch (action)
            {
                // farm stuff
                case "fish":
                    await FarmEngine.RunUserActionAsync(
                        component,
                        () => FarmEngine.CatchFishFunc(component, component.User));
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
                    await FarmEngine.RunUserActionAsync(
                        component,
                        () => Farm.PlantSeed(component, component.User));
                    break;
                case "crops_water":
                    await FarmEngine.RunUserActionAsync(
                        component,
                        () => Farm.WaterCrops(component, component.User));
                    break;
                case "crops_harvest":
                    await FarmEngine.RunUserActionAsync(
                        component,
                        () => Farm.HarvestCrops(component, component.User));
                    break;

                case "plots_repair":
                    await FarmEngine.RunUserActionAsync(
                        component,
                        () => Farm.RestorePlots(component, component.User));
                    break;

                case "pet_view":
                    await PetEngine.ViewPet(component, component.User);
                    break;
                case "pet_namechange":
                    await PetEngine.BeginNameChange(component);
                    break;

                // if nothing else, it's poll stuff
                default:
                    await component.RespondAsync(await PollEngine.VoteInPoll(component.User.Id, component.Message, component.Data.CustomId), ephemeral: true);
                    break;
            }
        });
    }

    private Task OnModalSubmitted(SocketModal modal)
    {
        return RunInteractionHandler(modal, async () =>
        {
            List<SocketMessageComponentData> components = modal.Data.Components.ToList();
            switch (modal.Data.CustomId)
            {
                case "pollMaker":
                {
                    await AdminCommands.FollowUpPoll(
                        question: components.First(x => x.CustomId == "question").Value, 
                        option1: components.First(x => x.CustomId == "option1").Value, 
                        option2: components.First(x => x.CustomId == "option2").Value, 
                        option3: components.First(x => x.CustomId == "option3").Value, 
                        option4: components.First(x => x.CustomId == "option4").Value, 
                        modal
                    );
                    return;
                }
                case "petNamechange":
                {
                    await FarmEngine.RunUserActionAsync(
                        modal,
                        () => PetEngine.SetPetName(modal, components.First(x => x.CustomId == "newName").Value));
                    return;
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

                    await MiscCommands.FollowUpReminder(time, unit, message, modal);
                    return;
                }
            }
        });
    }

    private Task OnMenuSelectionMade(SocketMessageComponent component)
    {
        return RunInteractionHandler(component, async () =>
        {
            switch (component.Data.CustomId)
            {
                case "buy" or "sell" or "sell_e":
                    string isAllowed = await FarmEngine.CanUseFarmComponent(component);
                    if (isAllowed != "yes")
                    {
                        await component.RespondAsync(isAllowed, ephemeral: true);
                        return;
                    }
                    await FarmEngine.RunUserActionAsync(component, () => FarmEngine.ShopAction(component));
                    break;
                case "defaultPet":
                    await FarmEngine.RunUserActionAsync(component, () => PetEngine.SetDefaultPet(component));
                    break;
            }

            if (component.Data.CustomId.Contains("petFeed_"))
            {
                await FarmEngine.RunUserActionAsync(component, () => PetEngine.FeedAPet(component));
            }
        });
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
