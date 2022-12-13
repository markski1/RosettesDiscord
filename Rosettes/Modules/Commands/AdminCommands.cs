using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Modules.Engine;

namespace Rosettes.Modules.Commands
{
    public class AdminCommands : InteractionModuleBase<SocketInteractionContext>
    {

        [SlashCommand("makevote", "Creates a quick, simple vote with thumbs up and down.")]
        public async Task NewVote(string question)
        {
            if (Context.Guild is null)
            {
                await RespondAsync("You may only create polls within a guild.");
                return;
            }

            await RespondAsync("Vote will be made.", ephemeral: true);

            string displayName;
            SocketGuildUser? GuildUser = Context.User as SocketGuildUser;
            if (GuildUser is not null && GuildUser.Nickname is not null)
            {
                displayName = GuildUser.Nickname;
            }
            else
            {
                displayName = Context.User.Username;
            }

            var embed = new EmbedBuilder();

            embed.AddField($"[{displayName}] created a vote:", question);

            var mid = await ReplyAsync(embed: embed.Build());

            var emojiList = new List<Emoji>
            {
                new Emoji("👍"),
                new Emoji("👎")
            };

            await mid.AddReactionsAsync(emojiList);
        }

        [SlashCommand("makepoll", "Creates a poll with up to 4 options.")]

        public async Task MakePoll(string question, string option1, string option2, string option3="NOT_PROVIDED", string option4= "NOT_PROVIDED")
        {
            if (Context.Guild is null)
            {
                await RespondAsync("You may only create polls within a guild.");
                return;
            }

            await RespondAsync("Poll will be made.", ephemeral: true);

            // prevent option 4 with no option 3
            if (option3 == "NOT_PROVIDED" && option4 != "NOT_PROVIDED")
            {
                option3 = option4;
                option4 = "NOT_PROVIDED";
            }

            string displayName;
            SocketGuildUser? GuildUser = Context.User as SocketGuildUser;
            if (GuildUser is not null && GuildUser.Nickname is not null)
            {
                displayName = GuildUser.Nickname;
            }
            else
            {
                displayName = Context.User.Username;
            }

            var embed = new EmbedBuilder();

            embed.AddField($"[{displayName}] created a poll:", question);

            string options = $"1. {option1}\n2. {option2}";
            var emojiList = new List<Emoji>
            {
                new Emoji("1️⃣"),
                new Emoji("2️⃣")
            };

            if (option3 != "NOT_PROVIDED")
            {
                options += $"\n3. {option3}";
                emojiList.Add("3️⃣");
            }
            if (option4 != "NOT_PROVIDED")
            {
                options += $"\n4. {option4}";
                emojiList.Add("4️⃣");
            }

            embed.AddField("Please choose: ", options);

            var mid = await ReplyAsync(embed: embed.Build());

            await mid.AddReactionsAsync(emojiList);
        }

        [SlashCommand("setautorole", "Sets the desired autoroles where used. Must first be created in the web panel.")]
        public async Task SetAutoRoles(uint code)
        {
            if (Context.Guild is null)
            {
                await RespondAsync("This command must run in a guild.");
                return;
            }
            if (!Global.CheckSnep(Context.User.Id) && Context.User != Context.Guild.Owner)
            {
                await RespondAsync("This command may only be used by the server owner.", ephemeral: true);
                return;
            }

            await AutoRolesEngine.SyncWithDatabase();

            var roles = AutoRolesEngine.GetRolesByCode(code, Context.Guild.Id);

            if (roles is null || !roles.Any())
            {
                await RespondAsync("Error. Please make sure you're using the right code in the right guild.", ephemeral: true);
                return;
            }

            List<Emoji> emojis = new();

            string text = "";

            var dbGuild = await GuildEngine.GetDBGuild(Context.Guild);
            var restGuild = await dbGuild.GetDiscordRestReference();
            var socketGuild = dbGuild.GetDiscordSocketReference();

            var embed = new EmbedBuilder();

            embed.WithTitle(AutoRolesEngine.GetNameFromCode(code));
            embed.WithDescription(" ");

            foreach (var role in roles)
            {
                emojis.Add(new Emoji(role.Emote));
                string roleName = "";
                if (socketGuild is not null && socketGuild.GetRole(role.RoleId) is not null)
                {
                    roleName = socketGuild.GetRole(role.RoleId).Name;
                } else if (restGuild is not null && restGuild.GetRole(role.RoleId) is not null)
                {
                    roleName = restGuild.GetRole(role.RoleId).Name;
                }
                text += $"{role.Emote} - {roleName}\n\n";
            }

            embed.AddField("Available roles: ", text);

            var mid = await ReplyAsync(embed: embed.Build());

            await mid.AddReactionsAsync(emojis);

            await GuildEngine.UpdateGuild(dbGuild);

            await AutoRolesEngine.UpdateGroupMessageId(code, mid.Id);

            await RespondAsync("Done!", ephemeral: true);
        }
    }
}