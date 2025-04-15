using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Server7
{
    public class SteamUser
    {
        [JsonProperty("steamid")]
        public string SteamId { get; set; }

        [JsonProperty("personaname")]
        public string PersonaName { get; set; }

        [JsonProperty("profileurl")]
        public string ProfileUrl { get; set; }

        [JsonProperty("avatarmedium")]
        public string Avatar { get; set; }
    }

    public class SteamResponse
    {
        [JsonProperty("response")]
        public SteamUserResponse Response { get; set; }
    }

    public class SteamUserResponse
    {
        [JsonProperty("players")]
        public List<SteamUser> Players { get; set; }
    }

    internal class Bot
    {
        public static readonly HttpClient STATIC_HTTP_CLIENT = new HttpClient();

        public static DiscordSocketClient discordClient;

        public static SocketTextChannel socketTextChannel;

        private static readonly HttpMethod PATCH = new HttpMethod("PATCH");

        private async Task RegisterCommandsAsync(SocketGuild socketGuild)
        {
            try
            {
                var to = new SlashCommandBuilder()
                    .WithName("to")
                    .AddOption("message", ApplicationCommandOptionType.String, "message")
                    .WithDescription("Send message to 7d2d server")
                    .WithDefaultMemberPermissions(GuildPermission.ManageGuild)
                    .Build();

                var shutdown = new SlashCommandBuilder()
                    .WithName("shutdown")
                    .AddOption("areyousure", ApplicationCommandOptionType.Boolean, "areyousure")
                    .WithDescription("Shutdown the 7d2d server")
                    .WithDefaultMemberPermissions(GuildPermission.ManageGuild)
                    .Build();

                discordClient.SlashCommandExecuted += HandleInteractionAsync;

                await socketGuild.CreateApplicationCommandAsync(to);
                await socketGuild.CreateApplicationCommandAsync(shutdown);
            }
            catch (Exception ex)
            {
                Log.Error($"RegisterCommandsAsync Exception: {ex.Message}");
            }
        }

        private async Task HandleInteractionAsync(SocketSlashCommand command)
        {
            try
            {
                switch (command.Data.Name)
                {
                    case "to":
                        if (command.Data.Options.Count > 0 && command.Data.Options.First().Name == "message")
                        {
                            var author = command.User;
                            List<int> ids = new List<int>();

                            foreach (var item in GameManager.Instance.World.Players.list)
                            {
                                ids.Add(item.entityId);
                            }

                            GameManager.Instance.ChatMessageServer(null, EChatType.Global, -1,
                                "[E74C3C]" + author.Username + "[-] [BBAA00]➜[-] " + command.Data.Options.First().Value,
                                ids, EMessageSender.None);

                            await command.RespondAsync(text: author.Username + " ➜ " + command.Data.Options.First().Value);
                        }
                        break;

                    case "shutdown":
                        if (command.Data.Options.Count > 0 && ((bool)command.Data.Options.First().Value))
                        {
                            if (Server7.telnetPassword == null || Server7.telnetPort == 0)
                            {
                                await command.RespondAsync(text: "Check whether telnet is enabled and password and port is set\nif not, shutdown command issued from discord won't work");
                                return;
                            }

                            await command.RespondAsync(text: "shutdown command issued");
                            new TelnetClient("localhost", Server7.telnetPort).Shutdown7d2dServer(Server7.telnetPassword);
                        }
                        else
                        {
                            await command.RespondAsync(text: "shutdown skipped");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"HandleInteractionAsync Exception: {ex.Message}");
                try
                {
                    await command.RespondAsync(text: "Ocorreu um erro ao executar o comando.");
                }
                catch (Exception ex2)
                {
                    Log.Error($"HandleInteractionAsync Exception: {ex2.Message}");
                }
            }
        }

        private async Task OnReadyAsync()
        {
            try
            {
                socketTextChannel = await discordClient.GetChannelAsync(Server7.settings.ChatChannelId) as SocketTextChannel;
                await RegisterCommandsAsync(socketTextChannel.Guild);
            }
            catch (Exception ex)
            {
                Log.Error($"OnReadyAsync Exception: {ex.Message}");
            }
        }

        public async Task RunBotAsync(string token)
        {
            try
            {
                var config = new DiscordSocketConfig
                {
                    GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildIntegrations | GatewayIntents.GuildMessages
                };

                discordClient = new DiscordSocketClient(config);

                discordClient.Log += Log2;
                discordClient.Ready += OnReadyAsync;

                await discordClient.LoginAsync(TokenType.Bot, token);
                await discordClient.StartAsync();
                await discordClient.SetCustomStatusAsync(Server7.settings.StartMessage);

            }
            catch (Exception ex)
            {
                Log.Error($"RunBotAsync Exception: {ex.Message}");
            }
        }

        private Task Log2(LogMessage arg)
        {
            try
            {
                Log.Out(arg.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Log2 Exception: " + ex.Message);
            }

            return Task.CompletedTask;
        }

        public static async Task<string> FetchSteamAvatarAsync(string steamId)
        {
            try
            {
                HttpResponseMessage response = await GetAsync("https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key="
                    + Server7.settings.SteamWebApiKey + "&steamids=" + steamId);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();

                    SteamResponse steamResponse = JsonConvert.DeserializeObject<SteamResponse>(jsonResponse);

                    if (steamResponse?.Response?.Players != null && steamResponse.Response.Players.Count > 0)
                    {
                        string avatar =  steamResponse.Response.Players[0].Avatar;
                        Server7.steamAvatarDict.Add(steamId, avatar);
                        return avatar;
                    }
                    else
                    {
                        return null;
                    }
                }
                return null;

            }
            catch (Exception ex)
            {
                Log.Error($"FetchSteamAsync Exception: {ex.Message}");
                throw;
            }
        }

        public static async Task SetAppDescription(HttpClient discordHttpClient, StringContent content)
        {
            try
            {
                if (discordClient == null)
                    return;

                await PatchAsync(discordHttpClient, "https://discord.com/api/v9/applications/" + discordClient.CurrentUser.Id, content);
            }
            catch (Exception ex)
            {
                Server7.cts.Cancel();
                Log.Warning($"SetAppDescription Exception: {ex.Message}");
                Log.Warning("This may be happened due to an invalid BotToken in Server7/Config/settings.json");
                Log.Warning("But it could happen if discord bot suddenly disconnects for a short period or lag");
            }
        }

        private static async Task<HttpResponseMessage> GetAsync(string requestUri)
        {
            try
            {
                var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

                return await STATIC_HTTP_CLIENT.SendAsync(requestMessage);
            }
            catch (Exception ex)
            {
                Log.Error($"GetAsync Exception: {ex.Message}");
                throw;
            }
        }

        private static async Task<HttpResponseMessage> PatchAsync(HttpClient discordHttpClient, string requestUri, HttpContent content)
        {
            try
            {
                var requestMessage = new HttpRequestMessage(PATCH, requestUri)
                {
                    Content = content
                };

                return await discordHttpClient.SendAsync(requestMessage);
            }
            catch (Exception ex)
            {
                Log.Error($"PatchAsync Exception: {ex.Message}");
                throw;
            }
        }
    }
}
