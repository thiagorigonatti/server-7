using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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

        [JsonProperty("loccountrycode")]
        public string Country { get; set; }
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
        private static HttpClient httpClient;
        public static DiscordSocketClient client;

        private async Task RegisterCommandsAsync()
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

                client.SlashCommandExecuted += HandleInteractionAsync;

                var channel = client.GetChannel(Server7.settings.ChatChannelId) as SocketTextChannel;

                await channel.Guild.CreateApplicationCommandAsync(to);
                await channel.Guild.CreateApplicationCommandAsync(shutdown);
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
                await RegisterCommandsAsync();
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

                client = new DiscordSocketClient(config);

                client.Log += Log2;
                client.Ready += OnReadyAsync;

                await client.LoginAsync(TokenType.Bot, token);
                await client.StartAsync();
                await client.SetCustomStatusAsync(Server7.settings.StartMessage);
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

        public static async Task<SteamUser> FetchSteamAsync(string steamId)
        {
            try
            {
                httpClient = new HttpClient();

                HttpResponseMessage response = await GetAsync(httpClient, "https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key="
                    + Server7.settings.SteamWebApiKey + "&steamids=" + steamId);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();

                    SteamResponse steamResponse = JsonConvert.DeserializeObject<SteamResponse>(jsonResponse);

                    if (steamResponse?.Response?.Players != null && steamResponse.Response.Players.Count > 0)
                    {
                        return steamResponse.Response.Players[0];
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

        public static async void SetAppDescription()
        {
            try
            {
                httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", Server7.settings.BotToken);

                var requestBody = new
                {
                    description = "Developed by TheCoders™:\nhttps://discord.gg/ntaUvVKYRC"
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                if (client == null)
                    return;

                await PatchAsync(httpClient, "https://discord.com/api/v9/applications/" + client.CurrentUser.Id, content);
            }
            catch (Exception ex)
            {
                Server7.cts.Cancel();
                Log.Error($"SetAppDescription Exception: {ex.Message}");
                Log.Error("This may be happened due to an invalid BotToken in Server7/Config/settings.json");
                Log.Error("Shutting Down Now.");
                new TelnetClient("localhost", Server7.telnetPort).Shutdown7d2dServer(Server7.telnetPassword);
            }
        }

        private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string requestUri)
        {
            try
            {
                var requestMessage = new HttpRequestMessage(new HttpMethod("GET"), requestUri);

                return await client.SendAsync(requestMessage);
            }
            catch (Exception ex)
            {
                Log.Error($"GetAsync Exception: {ex.Message}");
                throw;
            }
        }

        private static async Task<HttpResponseMessage> PatchAsync(HttpClient client, string requestUri, HttpContent content)
        {
            try
            {
                var requestMessage = new HttpRequestMessage(new HttpMethod("PATCH"), requestUri)
                {
                    Content = content
                };

                return await client.SendAsync(requestMessage);
            }
            catch (Exception ex)
            {
                Log.Error($"PatchAsync Exception: {ex.Message}");
                throw;
            }
        }
    }
}
