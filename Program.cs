using Discord.WebSocket;
using Discord;
using System.Text.Json;
using System.Diagnostics;

namespace Confiote
{
    public sealed class Program
    {
        private readonly DiscordSocketClient _client = new(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds
        });

        public static async Task Main()
        {
            await new Program().StartAsync();
        }

        private readonly HttpClient _http = new();

        public async Task StartAsync()
        {
            await Log.LogAsync(new LogMessage(LogSeverity.Info, "Setup", "Initialising bot"));

            // Setting Logs callback
            _client.Log += Log.LogAsync;

            // Load credentials
            if (!File.Exists("Keys/Credentials.json"))
                throw new FileNotFoundException("Missing Credentials file");
            var credentials = JsonSerializer.Deserialize<Credentials>(File.ReadAllText("Keys/Credentials.json"));
            if (credentials == null || credentials.BotToken == null)
                throw new NullReferenceException("Missing credentials");

            _client.Ready += Ready;
            _client.SlashCommandExecuted += SlashCommandExecuted;

            await _client.LoginAsync(TokenType.Bot, credentials.BotToken);
            await _client.StartAsync();

            // We keep the bot online
            await Task.Delay(-1);
        }

        private async Task SlashCommandExecuted(SocketSlashCommand arg)
        {
            try
            {
                switch (arg.CommandName)
                {
                    case "jam":
                        if (arg.User.Id != 144851584478740481) // TODO
                        {
                            await arg.RespondAsync("You don't have the perms for that", ephemeral: true);
                            break;
                        }
                        if (arg.Channel is not IGuildChannel guildChan)
                        {
                            await arg.RespondAsync("This can only be done in a guild", ephemeral: true);
                            break;
                        }

                        await arg.DeferAsync();

                        string name = (string)arg.Data.Options.FirstOrDefault(x => x.Name == "name")!.Value;
                        string start = (string)arg.Data.Options.FirstOrDefault(x => x.Name == "start")!.Value;
                        string end = (string)arg.Data.Options.FirstOrDefault(x => x.Name == "end")!.Value;
                        string link = (string)arg.Data.Options.FirstOrDefault(x => x.Name == "link")!.Value;

                        // Create category and channels
                        var cat = await guildChan.Guild.CreateCategoryAsync(name);
                        var info = await guildChan.Guild.CreateTextChannelAsync("info", p =>
                        {
                            p.CategoryId = cat.Id;
                        });
                        await guildChan.Guild.CreateTextChannelAsync("general", p =>
                        {
                            p.CategoryId = cat.Id;
                        });

                        // Create event
                        var e = await guildChan.Guild.CreateEventAsync(name, DateTime.Parse(start), GuildScheduledEventType.External, endTime: DateTime.Parse(end), location: link);

                        // Create associated role
                        var role = await guildChan.Guild.CreateRoleAsync(name, null, null, false, null);

                        // Set channel permissions
                        await cat.AddPermissionOverwriteAsync(guildChan.Guild.EveryoneRole, new(viewChannel: PermValue.Deny));
                        await cat.AddPermissionOverwriteAsync(role, new(viewChannel: PermValue.Allow, manageChannel: PermValue.Allow, manageMessages: PermValue.Allow));

                        // Send important info in #info channel
                        await info.SendMessageAsync(link);
                        await info.SendMessageAsync($"https://discord.com/events/{e.GuildId}/{e.Id}");

                        await arg.FollowupAsync("Event created!");
                        break;

                    case "ping":
                        await arg.RespondAsync($"Pong!\n{(DateTime.Now - arg.CreatedAt).TotalMilliseconds}ms", ephemeral: true);
                        break;

                    default:
                        throw new NotImplementedException();
                }

            }
            catch (Exception ex)
            {
                await arg.RespondAsync($"An error occured:\n```\n{ex}\n```", ephemeral: true);
            }
        }

        private bool _isInit = false;
        private async Task Ready()
        {
            if (!_isInit)
            {
                _isInit = true;
                _ = Task.Run(async () =>
                {
                    var commands = new SlashCommandBuilder[]
                    {
                        new()
                        {
                            Name = "ping",
                            Description = "Ping the bot"
                        },
                        new()
                        {
                            Name = "jam",
                            Description = "Create a new jam",
                            Options = new()
                            {
                                new() {
                                    Name = "name",
                                    Description = "Name of the jam",
                                    IsRequired = true,
                                    Type = ApplicationCommandOptionType.String
                                },
                                new() {
                                    Name = "start",
                                    Description = "Start yyyy-MM-dd HH:mm:ss",
                                    IsRequired = true,
                                    Type = ApplicationCommandOptionType.String
                                },
                                new() {
                                    Name = "end",
                                    Description = "End yyyy-MM-dd HH:mm:ss",
                                    IsRequired = true,
                                    Type = ApplicationCommandOptionType.String
                                },
                                new() {
                                    Name = "link",
                                    Description = "Link",
                                    IsRequired = true,
                                    Type = ApplicationCommandOptionType.String
                                }
                            }
                        }
                    }.Select(x => x.Build()).ToArray();
                    foreach (var cmd in commands)
                    {
                        if (Debugger.IsAttached)
                        {
                            // TODO: Don't hardcode
                            await _client.GetGuild(832001341865197579).CreateApplicationCommandAsync(cmd);
                        }
                        else
                        {
                            await _client.CreateGlobalApplicationCommandAsync(cmd);
                        }
                    }
                    if (Debugger.IsAttached)
                    {
                        await _client.GetGuild(832001341865197579).BulkOverwriteApplicationCommandAsync(commands);
                    }
                    else
                    {
                        await _client.BulkOverwriteGlobalApplicationCommandsAsync(commands);
                    }
                });
            }
        }
    }
}