using Discord;
using Discord.Interactions;
using Genbox.Wikipedia;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PokeApiNet;
using Rosettes.Core;
using Rosettes.Modules.Engine;
using System.Text.RegularExpressions;

namespace Rosettes.Modules.Commands.Utility;

[Group("find", "Commands to find certain things")]
public class FindCommands : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("pokemon", "Search for information about a specified pokémon")]
    public async Task GetPokemon([Summary("name-or-id", "Enter the name or ID for the Pokémon to search for.")] string name = "none")
    {
        bool searchById = int.TryParse(name, out int id);

        Pokemon getPokemon;

        try
        {
            if (searchById) getPokemon = await Global.PokeClient.GetResourceAsync<Pokemon>(id);
            else getPokemon = await Global.PokeClient.GetResourceAsync<Pokemon>(name);
        }
        catch
        {
            await RespondAsync("Sorry, I could not find a Pokémon by that name or ID.", ephemeral: true);
            return;
        }

        var dbUser = await UserEngine.GetDBUser(Context.User);

        EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

        embed.Title = $"{getPokemon.Name} ({getPokemon.Id})";

        string typesStr = "";

        foreach (var type in getPokemon.Types)
        {
            if (getPokemon.Types.First() != type)
            {
                typesStr += ", ";
            }
            typesStr += type.Type.Name;
        }

        embed.Description = $"**Types:** {typesStr}";

        embed.ThumbnailUrl = getPokemon.Sprites.FrontDefault;

        embed.AddField("Height", $"{getPokemon.Height * 10} centimeters", true);

        embed.AddField("Weight", $"{getPokemon.Weight * 100 / 1000f} kg", true);

        embed.AddField("Species", $"{getPokemon.Species.Name}", true);

        string stats = "";

        foreach (var stat in getPokemon.Stats)
        {
            stats += $"**{stat.Stat.Name}:** {stat.BaseStat}\n";
        }

        embed.AddField("Base stats", stats);

        if (getPokemon.BaseExperience is not null)
            embed.AddField("Base experience", $"{getPokemon.BaseExperience}", true);

        string forms = "";

        foreach (var form in getPokemon.Forms)
        {
            forms += $"{form.Name}\n";
        }

        embed.AddField("Forms", forms, true);

        string abilities = "";

        foreach (var ability in getPokemon.Abilities)
        {
            abilities += $"{ability.Ability.Name}\n";
        }

        embed.AddField("Abilities", abilities, true);

        if (getPokemon.HeldItems.Any())
        {
            string mayHold = "";
            foreach (var item in getPokemon.HeldItems)
            {
                mayHold += $"{item.Item.Name}\n";
            }
            embed.AddField("May be holding these items", mayHold, true);
        }


        EmbedFooterBuilder footer = new() { Text = "Data from PokeApi.co" };

        ComponentBuilder comps = new();

        comps.WithButton("PokémonDB Pokedex", style: ButtonStyle.Link, url: $"https://pokemondb.net/pokedex/{getPokemon.Name}");

        embed.Footer = footer;

        await RespondAsync(embed: embed.Build(), components: comps.Build());
    }

    [SlashCommand("urban", "Finds an UrbanDictionary definition for the provided word.")]
    public async Task UrbanDefine(string query)
    {
        if (Context.Guild is null)
        {
            await RespondAsync("This command cannot be used in DM's.");
            return;
        }

        if (!Regex.IsMatch(query, "^[a-zA-Z0-9 ]*$"))
        {
            await RespondAsync($"The term must only contain letters and numbers.");
            return;
        }

        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"https://mashape-community-urban-dictionary.p.rapidapi.com/define?term={query.ToLower()}"),
            Headers =
                {
                    { "X-RapidAPI-Key", Settings.RapidAPIKey },
                    { "X-RapidAPI-Host", "mashape-community-urban-dictionary.p.rapidapi.com" },
                },
        };

        try
        {
            using var response = await Global.HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadAsStringAsync();
            // obtain the definition from the API and parse it. Use "first" because it's all contained in a response object.
            var definitionList = JObject.Parse(data).First;
            if (definitionList is null)
            {
                await RespondAsync("Failed to fetch definitions.");
                return;
            }

            // "first" again because the definitions are inside a list (and as stated above, the list inside a response object. very stupid).
            definitionList = definitionList.First;

            // if the list is null, we have no results.
            if (definitionList is null)
            {
                await RespondAsync("No definition found for that word.");
                return;
            }

            List<dynamic> parsedDefinitionList = new();
            foreach (var aDefinition in definitionList)
            {
                dynamic? temp = JsonConvert.DeserializeObject(aDefinition.ToString());
                if (temp is null) continue;
                parsedDefinitionList.Add(temp);
            }

            var dbUser = await UserEngine.GetDBUser(Context.User);
            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            embed.Title = $"Definition for: {query}";

            if (parsedDefinitionList.Count > 0)
            {
                dynamic definition = parsedDefinitionList.OrderBy(def => def.thumbs_up - def.thumbs_down).Last();

                embed.Description = definition.definition;

                embed.AddField("Upvotes", $"{definition.thumbs_up}", inline: true);
                embed.AddField("Downvotes", $"{definition.thumbs_down}", inline: true);
                embed.AddField("Permalink", $"<{definition.permalink}>");
            }
            else
            {
                embed.Description = "Sorry, there does not seem to be any definition for that word.";
            }


            await RespondAsync(embed: embed.Build());
        }
        catch (Exception ex)
        {
            await RespondAsync("There was an error fetching the definition.", ephemeral: true);
            Global.GenerateErrorMessage("urbanDictionary", ex.Message);
        }
    }

    [SlashCommand("anime", "Finds a summary for a specified Anime.")]
    public async Task FindAnime(string name)
    {
        JikanDotNet.PaginatedJikanResponse<ICollection<JikanDotNet.Anime>> results;

        try
        {
            results = await Global.Jikan.SearchAnimeAsync(name) ?? throw new Exception();
        }
        catch
        {
            await RespondAsync("Sorry, there was an error conducting the search, or there were no results", ephemeral: true);
            return;
        }

        if (!results.Data.Any())
        {
            await RespondAsync("Sorry, there were no results for your search.", ephemeral: true);
            return;
        }

        var dbUser = await UserEngine.GetDBUser(Context.User);

        EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

        var result = results.Data.OrderBy(x => x.ScoredBy).Last();

        embed.Title = result.Titles.First().Title;

        embed.ThumbnailUrl = result.Images.JPG.ImageUrl;

        string themes = "";

        foreach (var theme in result.Themes)
        {
            themes += $"{theme.Name}\n";
        }

        if (themes.Length > 0) embed.AddField("Themes", themes, true);

        string genres = "";

        foreach (var genre in result.Genres)
        {
            genres += $"{genre.Name}\n";
        }

        if (genres.Length > 0) embed.AddField("Genres", genres);

        embed.AddField("Status", result.Status, true);

        embed.AddField("Type", result.Type, true);

        if (result.Aired.From is not null) embed.AddField("Aired since", result.Aired.From);
        if (result.Aired.To is not null) embed.AddField("Until", result.Aired.To);

        if (result.Episodes is not null) embed.AddField("Episodes", result.Episodes, true);

        embed.AddField("Duration", result.Duration, true);

        ComponentBuilder comps = new();

        comps.WithButton("MyAnimeList", style: ButtonStyle.Link, url: result.Url);

        if (result.Trailer is not null && result.Trailer.Url is not null)
            comps.WithButton("Trailer", style: ButtonStyle.Link, url: result.Trailer.Url);

        await RespondAsync(embed: embed.Build(), components: comps.Build());
    }

    [SlashCommand("manga", "Finds a summary for a specified Manga.")]
    public async Task FindManga(string name)
    {
        JikanDotNet.PaginatedJikanResponse<ICollection<JikanDotNet.Manga>> results;

        try
        {
            results = await Global.Jikan.SearchMangaAsync(name) ?? throw new Exception();
        }
        catch
        {
            await RespondAsync("Sorry, there was an error conducting the search, or there were no results", ephemeral: true);
            return;
        }

        if (!results.Data.Any())
        {
            await RespondAsync("Sorry, there were no results for your search.", ephemeral: true);
            return;
        }

        var dbUser = await UserEngine.GetDBUser(Context.User);

        EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

        var result = results.Data.OrderBy(x => x.ScoredBy).Last();

        embed.Title = result.Titles.First().Title;

        embed.ThumbnailUrl = result.Images.JPG.ImageUrl;

        string genres = "";
        foreach (var genre in result.Genres)
        {
            genres += $"{genre.Name}\n";
        }
        if (genres.Length > 0) embed.AddField("Genres", genres);

        string themes = "";
        foreach (var theme in result.Themes)
        {
            themes += $"{theme.Name}\n";
        }
        if (themes.Length > 0) embed.AddField("Themes", themes, true);

        embed.AddField("Status", result.Status, true);

        string authors = "";

        foreach (var author in result.Authors)
        {
            authors += $"{author.Name}\n";
        }

        embed.AddField("Author(s)", authors);

        if (result.Chapters is not null) embed.AddField("Chapters", result.Chapters, true);

        ComponentBuilder comps = new();

        comps.WithButton("MyAnimeList", style: ButtonStyle.Link, url: result.Url);

        await RespondAsync(embed: embed.Build(), components: comps.Build());
    }

    [SlashCommand("wiki", "Finds something on Wikipedia.")]
    public async Task FindWiki(string text)
    {
        using WikipediaClient client = new WikipediaClient();

        WikiSearchRequest req = new(text)
        {
            Limit = 1
        };

        try
        {
            WikiSearchResponse response = await client.SearchAsync(req) ?? throw new Exception("Failed to fetch results.");

            if (response.QueryResult is { SearchResults.Count: > 0 })
            {
                var use = response.QueryResult.SearchResults[0];

                EmbedAuthorBuilder author = new()
                {
                    Name = "Wikipedia",
                    IconUrl = "https://wikipedia.org/static/images/icons/wikipedia.png"
                };

                EmbedBuilder embed = new()
                {
                    Author = author,
                    Title = use.Title,
                    Description = use.Snippet ?? "No snippet available."
                };

                // remove HTML tags
                embed.Description = Regex.Replace(embed.Description, @"<[^>]+>|&nbsp;", "").Trim();

                ComponentBuilder comps = new();

                comps.WithButton("View on Wikipedia", style: ButtonStyle.Link, url: use.Url.AbsoluteUri);

                await RespondAsync(embed: embed.Build(), components: comps.Build());
            }
            else
            {
                await RespondAsync("Sorry, I found nothing with that text.", ephemeral: true);
            }
        }
        catch
        {
            await RespondAsync("Sorry, I could not make the search.", ephemeral: true);
        }
    }

    [SlashCommand("character", "Finds a summary for a specified character in media (Animated or Manga).")]
    public async Task FindCharacter(string name)
    {
        JikanDotNet.PaginatedJikanResponse<ICollection<JikanDotNet.Character>> results;

        try
        {
            results = await Global.Jikan.SearchCharacterAsync(name) ?? throw new Exception();
        }
        catch
        {
            await RespondAsync("Sorry, there was an error conducting the search, or there were no results.", ephemeral: true);
            return;
        }

        if (!results.Data.Any())
        {
            await RespondAsync("Sorry, there were no results for your search.", ephemeral: true);
            return;
        }

        var dbUser = await UserEngine.GetDBUser(Context.User);

        EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

        var result = results.Data.OrderBy(x => x.Favorites).Last();

        embed.Title = result.Name;

        if (result.Nicknames.Any())
        {
            string nicknames = "";
            foreach (var nickname in result.Nicknames)
            {
                if (result.Nicknames.First() != nickname) nicknames += "; ";
                nicknames += nickname;
            }
            embed.Description = $"Nicknames: {nicknames}";
        }

        embed.ThumbnailUrl = result.Images.JPG.ImageUrl;

        if (result.About is not null && result.About.Length > 0)
        {
            string hygienizedAbout;

            if (result.About.Length > 256)
            {
                hygienizedAbout = $"{result.About[0..256]} [...]";
            }
            else
            {
                hygienizedAbout = result.About;
            }

            embed.AddField("About", hygienizedAbout);
        }

        ComponentBuilder comps = new();

        comps.WithButton("MyAnimeList", style: ButtonStyle.Link, url: result.Url);

        await RespondAsync(embed: embed.Build(), components: comps.Build());
    }
}
