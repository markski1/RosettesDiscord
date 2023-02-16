using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Rosettes.Modules.Commands;
using Rosettes.Modules.Engine;
using System.ComponentModel;
using System;
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
            string action = component.Data.CustomId;

            if (action.Contains("null_")) return;

            // settings stuff
            if (action.Contains("toggle_"))
            {
                AdminHelper.ChangeSettings(component);
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
                    await FarmEngine.ShowPets(component, component.User);
                    break;
                case "farm":
                    await FarmEngine.ShowFarm(component, component.User);
                    break;

                case "crops_plant":
                    await FarmEngine.PlantPlot(component, component.User);
                    break;
                case "crops_water":
                    await FarmEngine.WaterPlots(component, component.User);
                    break;
                case "crops_harvest":
                    await FarmEngine.HarvestPlots(component, component.User);
                    break;


                // music stuff
                case "music_toggle": case "music_skip": case "music_stop": case "music_add":
                    if (component.GuildId is ulong guild_id)
                    {
                        var dbGuild = GuildEngine.GetDBGuildById(guild_id);
                        if (dbGuild != null)
                        {
                            var guild = dbGuild.GetDiscordSocketReference();
                            if (guild != null)
                            {
                                var dbUser = await UserEngine.GetDBUser(component.User);
                                EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);
                                embed.Title = "Music Player";
                                if (action == "music_toggle")
                                {
                                    embed.Description = await MusicEngine.ToggleAsync(guild);
                                }
                                if (action == "music_skip")
                                {
                                    embed.Description = await MusicEngine.SkipTrackAsync(guild);
                                }
                                if (action == "music_stop")
                                {
                                    embed.Description = await MusicEngine.StopAsync(guild);
                                    await component.Message.ModifyAsync(x => x.Components = new ComponentBuilder().Build());
                                }
                                if (action == "music_add")
                                {
                                    ModalBuilder modal = new()
                                    {
                                        Title = "Add song to queue",
                                        CustomId = "music_add"
                                    };
                                    modal.AddTextInput("Enter song name or URL", "song");
                                    await component.RespondWithModalAsync(modal.Build());
                                    return;
                                }
                                await component.Message.ModifyAsync(x => x.Embed = embed.Build());
                                await component.DeferAsync();
                            }
                        }
                    }
                    break;



                // if nothing else, it's poll stuff
                default:
                    await component.RespondAsync(await PollEngine.VoteInPoll(component.User.Id, component.Message, component.Data.CustomId), ephemeral: true);
                    break;
            }
        }

        private async Task OnModalSubmitted(SocketModal modal)
        {
            List<SocketMessageComponentData> components = modal.Data.Components.ToList();
            if (modal.Data.CustomId == "pollMaker")
            {
                string question = components.First(x => x.CustomId == "question").Value;
                string option1 = components.First(x => x.CustomId == "option1").Value;
                string option2 = components.First(x => x.CustomId == "option2").Value;
                string option3 = components.First(x => x.CustomId == "option3").Value;
                string option4 = components.First(x => x.CustomId == "option4").Value;

                await AdminCommands.FollowUpPoll(question, option1, option2, option3, option4, modal);
                return;
            }
            
            if (modal.Data.CustomId == "music_add")
            {
                if (modal.GuildId is ulong guild_id)
                {
                    var dbGuild = GuildEngine.GetDBGuildById(guild_id);
                    if (dbGuild != null)
                    {
                        var guild = dbGuild.GetDiscordSocketReference();
                        if (guild != null)
                        {
                            var dbUser = await UserEngine.GetDBUser(modal.User);
                            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);
                            embed.Title = "Music player";
                            if (modal.User is not SocketGuildUser _socketUser || modal.User is not IVoiceState _voiceState || modal.Channel is not ITextChannel _textChannel)
                            {
                                return;
                            }
                            embed.Description = await MusicEngine.PlayAsync(_socketUser, guild, _voiceState, _textChannel, components.First(x => x.CustomId == "song").Value);
                            var ogMessage = MusicEngine.GetPlayer(modal.Channel);
                            if (ogMessage is not null)
                            {
                                await ogMessage.ModifyAsync(x => x.Embed = embed.Build());
                            }

                            await modal.DeferAsync();
                        }
                    }
                }
            }
        }

        private async Task OnMenuSelectionMade(SocketMessageComponent component)
        {
            if (component.Data.CustomId is "buy" or "sell") {
                await FarmEngine.ShopAction(component);
            }
            if (component.Data.CustomId is "defaultPet")
            {
                await FarmEngine.SetDefaultPet(component);
            }
        }

        private Task OnCommandExecuted(SlashCommandInfo arg1, IInteractionContext arg2, IResult arg3)
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

            _commands.SlashCommandExecuted += OnCommandExecuted;
        }
    }
}
