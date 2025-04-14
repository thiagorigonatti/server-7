# [Server7](https://github.com/thiagorigonatti/server-7/)

## :bulb: What It Does:
- Live Server Stats in Real-Time
  - Get the current number of online players
  - See the in-game day, hour, and minute — all updated in real time!

- Server Chat Logging
  - Automatically logs all in-game chat messages to a designated Discord channel
  - Perfect for monitoring or keeping track of activity when you're offline

- Discord-to-Server Communication
  - Admins can send messages from Discord directly into the 7DTD server chat
  - Execute useful server commands from within Discord itself!

## :white_check_mark: Why Use It?
- Super easy to install and set up :tools:
- Keeps your community informed and connected
- Enhances admin control and server transparency
- Great for roleplay servers, community events, or just managing your world like a pro

```json
settings.json
{
  "BotToken": "YourBotTokenGoesHere",
  "StartMessage": "Starting...",
  "BotStatus": "%online_players_count% on, day %world_days% %week_day%, %world_hours%:%world_minutes% %blood_moon_icon%",
  "JoinMessage": "joined the game",
  "LeaveMessage": "left the game",
  "DeathMessage": "died",
  "AnnounceBloodMoon": true,
  "LocalWeekdays": [
    "Sun", 
    "Mon", 
    "Tue", 
    "Wed", 
    "Thu", 
    "Fri", 
    "Sat"
  ],
  "Period": 15,
  "ChatChannelId": 1234567891011121314,
  "SteamWebApiKey": "YourSteamWebApiKeyHere",
  "BloodMoonIcon": "🩸",
  "BloodMoonStartMessage": "Bloodmoon has started",
  "BloodMoonEndMessage": "Bloodmoon has ended"
}
```
```diff
- Only download it from official websites:
```
Download: https://github.com/thiagorigonatti/server-7/releases

or

Download: https://www.nexusmods.com/7daystodie/mods/7240

1. Extract it to your Mods folder.

2. Create a discord application and a bot in https://discord.com/developers/applications, copy the BotToken paste it in BotToken value area.

3. Invite the bot to your server.

4. Copy a textchannel id from the discord server your bot is in and paste in it's place.

5. To get your SteamWebApiKey use https://steamcommunity.com/dev/apikey; so paste it in SteamWebApiKey value area. `This is used to fetch steam and grab public players profile info, like nickname, avatar, country and custom url, then use them when discord logging players messages/commands issued in 7dtd server. This is a web api key and shall be secure, this one is not that one that can be used to trade items, so even if you are careless and leak it, don't worry, just revoke it in the same site where you created.`

6. Start your server.

Thank you for downloading it!