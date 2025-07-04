using Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Server7
{
    public class Settings
    {
        public string BotToken { get; set; }
        public string StartMessage { get; set; }
        public string BotStatus { get; set; }
        public string JoinMessage { get; set; }
        public string LeaveMessage { get; set; }
        public string DeathMessage { get; set; }
        public bool AnnounceBloodMoon { get; set; }

        public string[] LocalWeekdays { get; set; }

        public int Period { get; set; }
        public ulong ChatChannelId { get; set; }
        public string SteamWebApiKey { get; set; }
        public string BloodMoonIcon { get; set; }
        public string BloodMoonStartMessage { get; set; }
        public string BloodMoonEndMessage { get; set; }
    }

    public class Server7 : IModApi
    {
        private static readonly string serverChatName = "Server";
        public static Settings settings;
        private static bool gameStartDone = false;
        private static EmbedBuilder instance;
        public static string telnetPassword;
        public static int telnetPort;
        public static readonly CancellationTokenSource cts = new CancellationTokenSource();
        public static bool isBloodMoonVar = false;
        public static Dictionary<string, string> steamAvatarDict = new Dictionary<string, string>();

        private void ReadTelnetPassword()
        {
            try
            {
                XDocument xdoc = XDocument.Load("serverconfig.xml");

                var enabled = xdoc.Root.Elements("property").FirstOrDefault(e => (string)e.Attribute("name") == "TelnetEnabled");
                var pass = xdoc.Root.Elements("property").FirstOrDefault(e => (string)e.Attribute("name") == "TelnetPassword");
                var port = xdoc.Root.Elements("property").FirstOrDefault(e => (string)e.Attribute("name") == "TelnetPort");

                if (!(bool)enabled.Attribute("value") || string.IsNullOrEmpty((string)pass.Attribute("value")) || string.IsNullOrEmpty((string)port.Attribute("value")))
                {
                    Log.Warning("Check whether telnet is enabled and password and port is set");
                    Log.Warning("if not, shutdown command issued from discord won't work");
                    return;
                }

                telnetPassword = (string)pass.Attribute("value");
                telnetPort = (int)port.Attribute("value");
            }
            catch (Exception ex)
            {
                Log.Error($"ReadTelnetPassword Exception: {ex.Message}");
            }
        }

        private static EmbedBuilder GetEmbedBuilder()
        {
            try
            {
                if (instance == null)
                {
                    instance = ClearEmbed(new EmbedBuilder());
                }
                return ClearEmbed(instance);
            }
            catch (Exception ex)
            {
                Log.Error($"GetEmbedBuilder Exception: {ex.Message}");
                return new EmbedBuilder();
            }
        }

        private static EmbedBuilder ClearEmbed(EmbedBuilder embedBuilder)
        {
            try
            {
                embedBuilder.Description = null;
                embedBuilder.Color = null;
                embedBuilder.Author = null;
                return embedBuilder;
            }
            catch (Exception ex)
            {
                Log.Error($"ClearEmbed Exception: {ex.Message}");
                return embedBuilder;
            }
        }

        private void OnGameStartDone(ref ModEvents.SGameStartDoneData data)
        {
            try
            {
                gameStartDone = true;
                _ = SendStats("https://d18y02ttrwdv32.cloudfront.net/v1/stats");
            }
            catch (Exception ex)
            {
                Log.Error($"OnGameStartDone Exception: {ex.Message}");
            }
        }

        public enum EventType
        {
            Spawn,
            Leave,
            Death,
            Chat
        }

        private async Task EmbedEvent(ClientInfo clientInfo, EventType eventType, string message)
        {
            try
            {
                string steamId = clientInfo.PlatformId.ToString().Replace("Steam_", string.Empty);
                string avatar = steamAvatarDict.ContainsKey(steamId) ? steamAvatarDict[steamId] : await Bot.FetchSteamAvatarAsync(steamId);
                string profile = "https://steamcommunity.com/profiles/" + steamId;

                var embed = GetEmbedBuilder();

                switch (eventType)
                {
                    case EventType.Spawn:
                        embed.WithAuthor($"{clientInfo.playerName}", avatar, profile)
                             .WithDescription(settings.JoinMessage)
                             .WithColor(Color.Green);
                        break;

                    case EventType.Leave:
                        embed.WithAuthor($"{clientInfo.playerName}", avatar, profile)
                             .WithDescription(settings.LeaveMessage)
                             .WithColor(Color.LighterGrey);
                        break;

                    case EventType.Death:
                        embed.WithAuthor($"{clientInfo.playerName}", avatar, profile)
                             .WithDescription(settings.DeathMessage)
                             .WithColor(Color.DarkRed);
                        break;

                    case EventType.Chat:
                        embed.WithAuthor($"{clientInfo.playerName}:", avatar, profile)
                             .WithDescription(message)
                             .WithColor(Color.Gold);
                        break;
                }

                await Bot.socketTextChannel.SendMessageAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                Log.Error($"EmbedEvent Exception: {ex.Message}");
            }
        }

        // blood moon announcement to discord
        private async Task AnnounceBloodMoonToDiscord(string message)
        {
            try
            {
                var embed = GetEmbedBuilder();

                Color color = isBloodMoonVar ? Color.Red : Color.Blue;
                embed.WithAuthor("Server:")
                     .WithDescription(message)
                     .WithColor(color);
                await Bot.socketTextChannel.SendMessageAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                Log.Error($"AnnounceBloodMoonToDiscord Exception: {ex.Message}");
            }
        }

        private void OnPlayerSpawn(ref ModEvents.SPlayerSpawnedInWorldData data)
        {

            ClientInfo clientInfo = data.ClientInfo;
            RespawnType respawnType = data.RespawnType;

            try
            {
                if (clientInfo == null || (respawnType != RespawnType.JoinMultiplayer && respawnType != RespawnType.EnterMultiplayer) || !clientInfo.PlatformId.ToString().StartsWith("Steam_"))
                    return;

                _ = EmbedEvent(clientInfo, EventType.Spawn, null);

            }
            catch (Exception ex)
            {
                Log.Error($"OnPlayerSpawn Exception: {ex.Message}");
            }
        }

        private void OnPlayerDisconnect(ref ModEvents.SPlayerDisconnectedData data)
        {

            ClientInfo clientInfo = data.ClientInfo;

            try
            {
                if (clientInfo == null || !clientInfo.PlatformId.ToString().StartsWith("Steam_"))
                    return;

                _ = EmbedEvent(clientInfo, EventType.Leave, null);
            }
            catch (Exception ex)
            {
                Log.Error($"OnPlayerDisconnect Exception: {ex.Message}");
            }
        }

        private ModEvents.EModEventResult OnPlayerDeath(ref ModEvents.SGameMessageData data)
        {

            ClientInfo clientInfo = data.ClientInfo;
            EnumGameMessages enumGameMessages = data.MessageType;

            try
            {
                if (enumGameMessages != EnumGameMessages.EntityWasKilled || clientInfo == null || !clientInfo.PlatformId.ToString().StartsWith("Steam_"))
                    return ModEvents.EModEventResult.StopHandlersRunVanilla;

                _ = EmbedEvent(clientInfo, EventType.Death, null);
            }
            catch (Exception ex)
            {
                Log.Error($"OnPlayerDeath Exception: {ex.Message}");
            }

            return ModEvents.EModEventResult.StopHandlersRunVanilla;
        }

        private ModEvents.EModEventResult OnPlayerChat(ref ModEvents.SChatMessageData data)
        {


            ClientInfo clientInfo = data.ClientInfo;
            EChatType type = data.ChatType;
            string mainName = data.MainName;
            string message = data.Message;

            try
            {
                if (clientInfo == null || type != EChatType.Global || mainName == serverChatName || !clientInfo.PlatformId.ToString().StartsWith("Steam_") || string.IsNullOrEmpty(message))
                    return ModEvents.EModEventResult.StopHandlersRunVanilla;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await EmbedEvent(clientInfo, EventType.Chat, message);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"EmbedEvent async error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error($"OnPlayerChat Exception: {ex.Message}");
            }
            return ModEvents.EModEventResult.StopHandlersRunVanilla;
        }

        public async void InitMod(Mod mod)
        {
            try
            {
                ReadTelnetPassword();

                ModEvents.GameStartDone.RegisterHandler(OnGameStartDone);
                ModEvents.ChatMessage.RegisterHandler(OnPlayerChat);
                ModEvents.PlayerSpawnedInWorld.RegisterHandler(OnPlayerSpawn);
                ModEvents.PlayerDisconnected.RegisterHandler(OnPlayerDisconnect);
                ModEvents.GameMessage.RegisterHandler(OnPlayerDeath);

                var filePath = Path.Combine(AppContext.BaseDirectory + "/Mods/Server7/Config", "settings.json");

                if (!File.Exists(filePath))
                {
                    var defaultSettings = new Settings
                    {
                        BotToken = "YourBotTokenGoesHere",
                        StartMessage = "Starting...",
                        BotStatus = "%online_players_count% on, day %world_days% %week_day%, %world_hours%:%world_minutes% %blood_moon_icon%",
                        Period = 15,
                        ChatChannelId = 1234567891011121314,
                        JoinMessage = "joined the game",
                        LeaveMessage = "left the game",
                        DeathMessage = "died",
                        AnnounceBloodMoon = false,
                        BloodMoonStartMessage = "Bloodmoon has started",
                        BloodMoonEndMessage = "Bloodmoon has ended",
                        LocalWeekdays = new string[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" },
                        BloodMoonIcon = "🩸",
                        SteamWebApiKey = "YourSteamWebApiKeyHere"
                    };

                    var defaultJson = JsonConvert.SerializeObject(defaultSettings, Formatting.Indented);
                    File.WriteAllText(filePath, defaultJson);
                    Log.Out("settings.json created successfully");
                }

                var json = File.ReadAllText(filePath);
                settings = JsonConvert.DeserializeObject<Settings>(json);

                await Task.Run(async () =>
                {
                    await new Bot().RunBotAsync(settings.BotToken);
                });

                var task = RunEveryXSecondsAsync(cts.Token);
                await task;
            }
            catch (Exception ex)
            {
                Log.Error($"InitMod Exception: {ex.Message}");
            }
        }

        private async Task RunEveryXSecondsAsync(CancellationToken cancellationToken)
        {
            try
            {
                string previousStatus = string.Empty;

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (gameStartDone)
                    {
                        // Bloodmoon announcement logic
                        if (settings.AnnounceBloodMoon)
                        {
                            if (GameManager.Instance.World.isEventBloodMoon)
                            {
                                if (!isBloodMoonVar)
                                {
                                    isBloodMoonVar = true;
                                    _ = AnnounceBloodMoonToDiscord(settings.BloodMoonStartMessage);
                                }
                            }
                            else if (isBloodMoonVar)
                            {
                                isBloodMoonVar = false;
                                _ = AnnounceBloodMoonToDiscord(settings.BloodMoonEndMessage);
                            }
                        }

                        double percent = GameManager.Instance.World.worldTime / 1000D;
                        int minutes = (int)(percent * 60);
                        int finalMinutes = minutes % 60;

                        string hours = GameManager.Instance.World.WorldHour < 10 ? "0" + GameManager.Instance.World.WorldHour : GameManager.Instance.World.WorldHour.ToString();
                        string min = finalMinutes < 10 ? "0" + finalMinutes : finalMinutes.ToString();

                        // New weekday logic
                        string weekDay = string.Empty;
                        int dayIndex = GameManager.Instance.World.WorldDay % 7;
                        weekDay = settings.LocalWeekdays[dayIndex];

                        string newStatus = settings.BotStatus
                            .Replace("%online_players_count%", GameManager.Instance.World.Players.Count.ToString())
                            .Replace("%world_days%", GameManager.Instance.World.WorldDay.ToString())
                            .Replace("%week_day%", weekDay)
                            .Replace("%world_hours%", hours)
                            .Replace("%world_minutes%", min)
                            .Replace("%blood_moon_icon%", isBloodMoonVar ? settings.BloodMoonIcon : string.Empty);

                        if (!previousStatus.Equals(newStatus))
                        {
                            previousStatus = newStatus;
                            await Bot.discordClient.SetCustomStatusAsync(previousStatus);
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(settings.Period), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"RunEveryXSecondsAsync Exception: {ex.Message}");
            }
        }

        private string GetVersion()
        {
            try
            {
                var logDirectory = Path.Combine(AppContext.BaseDirectory);
                var logFiles = Directory.GetFiles(logDirectory, "output_log_*")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();

                if (!logFiles.Any())
                    return "'output_log_' file not found.";

                var latestLogFile = logFiles.First().FullName;
                var latestLogFileName = logFiles.First().Name;
                FileStream fs = new FileStream(latestLogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                string target = "INF Version: ";

                using (StreamReader reader = new StreamReader(fs))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Contains(target))
                        {
                            return latestLogFileName + "=" + line.Substring(line.IndexOf(target) + target.Length);
                        }
                    }
                }

                return $"Version not found in {Path.GetFileName(latestLogFile)}";
            }
            catch (Exception ex)
            {
                Log.Error($"GetVersion Exception: {ex.Message}");
                throw;
            }
        }

        private async Task<HttpResponseMessage> SendStats(string requestUri)
        {
            try
            {
                string[] data = GetVersion().Split('=');
                var requestBody = new
                {
                    file = data[0].Trim(),
                    version = data[1].Trim(),
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = content
                };

                return await Bot.STATIC_HTTP_CLIENT.SendAsync(requestMessage);
            }
            catch (Exception ex)
            {
                Log.Error($"SendStats Exception: {ex.Message}");
                throw;
            }
        }
    }
}