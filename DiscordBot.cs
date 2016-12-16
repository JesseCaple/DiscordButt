namespace DiscordButt
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Speech.AudioFormat;
    using System.Speech.Synthesis;
    using System.Threading.Tasks;
    using System.Timers;
    using System.Xml.Linq;
    using Beam;
    using Discord;
    using Discord.Audio;
    using Discord.Commands;
    using GoogleMusicApi.Common;
    using NAudio.Wave;
    using Newtonsoft.Json.Linq;

    public class DiscordBot
    {
        private readonly WitFactory wit;
        private readonly Random random;
        private readonly DiscordClient discord;
        private readonly BeamConnection beam;
        private readonly System.Timers.Timer timer;
        private readonly Config config;

        private ulong voiceChannelId;
        private ulong heartbeatChannelId;

        private IAudioClient audio;
        private bool readingMessages;
        private bool playingMusic;
        private Channel readingChannel;
        private Channel cloningChannel;

        public DiscordBot()
        {
            this.config = new Config();

            this.wit = new WitFactory();
            this.random = new Random();

            this.timer = new System.Timers.Timer(60000);
            this.timer.Enabled = false;
            this.timer.AutoReset = true;
            this.timer.Elapsed += this.OncePerMinute;

            this.discord = new DiscordClient(x =>
            {
                x.LogLevel = LogSeverity.Info;
                x.LogHandler = (s, e) =>
                {
                    Console.WriteLine($"{DateTime.Now} - [{e.Severity.ToString()[0]}] {e.Source}: {e.Message}");
                };
            });

            this.discord.UsingAudio(x =>
            {
                x.Mode = AudioMode.Outgoing;
                x.EnableEncryption = false;
            });

            this.discord.UsingCommands(x =>
            {
                x.PrefixChar = '.';
                x.AllowMentionPrefix = true;
                x.HelpMode = HelpMode.Public;
            });

            this.RegisterCommands();

            this.discord.MessageReceived += this.Discord_ChatMessage;

            this.beam = new BeamConnection();
            this.beam.OnChatMessage += this.Beam_OnChatMessage;

            this.readingMessages = false;
        }

        public void Start()
        {
            if (string.IsNullOrEmpty(this.config.DiscordKey))
            {
                Console.WriteLine("Please set discord key in configuration file.");
                Console.ReadKey();
                return;
            }

            if (string.IsNullOrEmpty(this.config.VoiceChannel) ||
                string.IsNullOrEmpty(this.config.HeartbeatChannel))
            {
                Console.WriteLine("Please set discord voice and heartbeat channel ids in configuration file.");
                Console.ReadKey();
                return;
            }

            this.voiceChannelId = ulong.Parse(this.config.VoiceChannel);
            this.heartbeatChannelId = ulong.Parse(this.config.HeartbeatChannel);

            this.discord.ExecuteAndWait(async () =>
            {
                await this.discord.Connect(this.config.DiscordKey, TokenType.Bot);
                this.timer.Enabled = true;
            });
        }

        private async Task InitVoice()
        {
            if (this.audio == null || this.audio.State == ConnectionState.Disconnected)
            {
                var voiceChannel = this.discord.GetChannel(this.voiceChannelId);
                this.audio = await voiceChannel.JoinAudio();
            }
        }

        private async void Discord_ChatMessage(object sender, MessageEventArgs e)
        {
            if (this.beam.IsConnected && e.User.Id != this.discord.CurrentUser.Id && e.Channel.Id == 187760090206437376)
            {
                var msg = $"[discord] {e.Message.User.Name}: {e.Message.Text}";
                await this.beam.SendMessage(msg);
            }

            if (this.readingMessages && e.Channel == this.readingChannel)
            {
                using (var synthesizer = new SpeechSynthesizer())
                using (var mem = new MemoryStream())
                {
                    var info = new SpeechAudioFormatInfo(48000, AudioBitsPerSample.Sixteen, AudioChannel.Stereo);
                    synthesizer.SetOutputToAudioStream(mem, info);

                    PromptBuilder builder = new PromptBuilder();
                    builder.Culture = CultureInfo.CreateSpecificCulture("en-US");
                    builder.StartVoice(builder.Culture);
                    builder.StartSentence();
                    builder.StartStyle(new PromptStyle() { Emphasis = PromptEmphasis.Reduced });
                    builder.AppendText(e.Message.User.Name);
                    builder.AppendText(" says ");
                    builder.EndStyle();
                    builder.AppendText(e.Message.Text);
                    builder.EndSentence();
                    builder.EndVoice();
                    synthesizer.Speak(builder);
                    mem.Seek(0, SeekOrigin.Begin);

                    int count, block = 96000;
                    var buffer = new byte[block];
                    while ((count = mem.Read(buffer, 0, block)) > 0)
                    {
                        if (count < block)
                        {
                            for (int i = count; i < block; i++)
                            {
                                buffer[i] = 0;
                            }
                        }

                        this.audio.Send(buffer, 0, block);
                    }
                }
            }
        }

        private void Beam_OnChatMessage(string username, string message)
        {
            if (username != "DiscordButt")
            {
                var msg = $"[beam.pro] {username}: {message}";
                this.cloningChannel.SendMessage(msg);
            }
        }

        private async void OncePerMinute(object sender, ElapsedEventArgs e)
        {
            await this.discord.GetChannel(this.heartbeatChannelId).SendIsTyping();
            if (this.random.NextDouble() <= .0009)
            {
                var channel = this.discord.Servers.First().DefaultChannel;
                var map = new Dictionary<int, string>()
                {
                    { 0, "pony" },
                    { 1, "doge" },
                    { 2, "cat" },
                };

                var query = map[this.random.Next(3)];
                await this.GifCommand(channel, query);
            }

            if (this.random.NextDouble() <= .0001)
            {
                var percent = this.random.Next(60, 99);
                var channel = this.discord.Servers.First().DefaultChannel;
                await channel.SendMessage($"Dank Levels holding at {percent}%.");
            }
        }

        private void RegisterCommands()
        {
            var service = this.discord.GetService<CommandService>();

            service.CreateCommand("video")
                .Alias("youtube", "yt", "vid")
                .Description("Posts the top video from YouTube.")
                .Parameter("query", ParameterType.Multiple)
                .Do(async x => await this.VideoCommand(x.Channel, string.Join("+", x.Args)));

            service.CreateCommand("image")
                .Alias("img")
                .Description("Posts an image from Google. Limited to 100 per day.")
                .Parameter("query", ParameterType.Multiple)
                .Do(async x => await this.ImageCommand(x.Channel, string.Join("+", x.Args)));

            service.CreateCommand("gif")
                .Description("Posts a random gif from giphy.")
                .Parameter("query", ParameterType.Multiple)
                .Do(async x => await this.GifCommand(x.Channel, string.Join("+", x.Args)));

            service.CreateCommand("cat")
                .Description("Posts a random cat picture.")
                .Do(async x => await this.CatCommand(x.Channel));

            service.CreateCommand("define")
                .Alias("definition")
                .Description("Posts the totally real definition of a word.")
                .Parameter("query", ParameterType.Multiple)
                .Do(async x => await this.DefineCommand(x.Channel, string.Join("+", x.Args)));

            service.CreateCommand("bullshit")
                .Alias("quote", "quotation")
                .Description("Posts some random bullshit quote no one cares about.")
                .Do(async x => await this.BullshitCommand(x.Channel));

            service.CreateCommand("shitpost")
                .Alias("imgur", "badmeme")
                .Description("Posts some shitty random imgur \"meme\".")
                .Do(async x => await this.ShitpostCommand(x.Channel));

            service.CreateCommand("lyrics")
                .Description("Posts lyrics from a song. Maybe from the song you want.")
                .Parameter("query", ParameterType.Multiple)
                .Do(async x => await this.LyricsCommand(x.Channel, string.Join("+", x.Args)));

            service.CreateCommand("roll")
                .Description("Rolls a random number from 1 to 100.")
                .Do(async x => await this.RollCommand(x.Channel, x.User));

            service.CreateCommand("time")
                .Description("Shows you the current time at the best and worst places.")
                .Do(async x => await this.TimeCommand(x.Channel));

            service.CreateCommand("beam")
                .Description("Toggles beam chat cloning.")
                .AddCheck(new AdminPermission())
                .Do(async x => await this.BeamCommand(x.Channel));

            service.CreateCommand("read")
                .Description("Toggles reading general in main voice channel.")
                .Do(async x => await this.ReadCommand(x.Server, x.Channel));

            service.CreateCommand("music")
                .Description("Plays a song from Google Play Music.")
                .Parameter("query", ParameterType.Multiple)
                .Do(async x => await this.MusicCommand(x.Channel, string.Join("+", x.Args)));

            service.CreateCommand("stop")
                .Description("Stops the currently playing music.")
                .Do(async x => await this.StopCommand(x.Channel, x.User));
        }

        private async Task StopCommand(Channel channel, User user)
        {
            await channel.SendIsTyping();

            if (this.playingMusic)
            {
                this.playingMusic = false;
                await Task.Delay(1000);
                await channel.SendMessage($"Music stopped by {user.Name}.");
            }
            else
            {
                await channel.SendMessage("No music is playing, bro.");
            }
        }

        private async Task MusicCommand(Channel channel, string query)
        {
            await channel.SendIsTyping();
            await this.InitVoice();

            if (this.readingMessages)
            {
                await channel.SendMessage("I'm busy reading shit.");
                return;
            }

            if (this.playingMusic)
            {
                await channel.SendMessage("No, I like this song.");
                return;
            }

            this.playingMusic = true;

            var mc = new MobileClient();
            if (await mc.LoginAsync(this.config.GoogleUsername, this.config.GooglePassword))
            {
                var result = await mc.SearchAsync(query);
                var entry = result.Entries.Where(x => x.Track != null).FirstOrDefault();
                if (entry != null)
                {
                    try
                    {
                        var track = entry.Track;
                        var uri = await mc.GetStreamUrlAsync(track);
                        var request = WebRequest.CreateHttp(uri);
                        using (var rsp = request.GetResponse())
                        using (var web = rsp.GetResponseStream())
                        using (var mem = new MemoryStream())
                        {
                            await channel.SendMessage("Buffering song...");
                            int count, block = 96000;
                            byte[] buffer = new byte[block];
                            while (this.playingMusic && (count = web.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                mem.Write(buffer, 0, count);
                            }

                            mem.Seek(0, SeekOrigin.Begin);
                            await channel.SendMessage($"\r\n{track.Artist}\r\n*{track.Album}*\r\n**{track.Title}**");
                            using (var mp3 = new Mp3FileReader(mem))
                            using (var wav = WaveFormatConversionStream.CreatePcmStream(mp3))
                            using (var aligned = new BlockAlignReductionStream(wav))
                            {
                                while (this.playingMusic && (count = aligned.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    this.audio.Send(buffer, 0, count);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        await channel.SendMessage($"Guess THIS is the day that the music died.");
                    }

                    this.playingMusic = false;
                }
                else
                {
                    this.playingMusic = false;
                    await channel.SendMessage("Sorry, never heard of that song.");
                }
            }
            else
            {
                this.playingMusic = false;
                await channel.SendMessage("Uhm, maybe config me better? kthx");
            }
        }

        private async Task ReadCommand(Server server, Channel channel)
        {
            await channel.SendIsTyping();
            await this.InitVoice();

            if (this.playingMusic && !this.readingMessages)
            {
                await channel.SendMessage("Not now, I'm playing some sickening tunes, yo.");
            }
            else if (this.readingMessages)
            {
                this.readingMessages = false;
                await channel.SendMessage("Reading messages **OFF**");
            }
            else
            {
                await channel.SendMessage("Reading messages **ON**");
                this.readingChannel = channel;
                this.readingMessages = true;
            }
        }

        private async Task BeamCommand(Channel channel)
        {
            if (this.beam.IsConnected)
            {
                await this.beam.Disconnect();
                await channel.SendMessage("Beam chat cloning **OFF**");
            }
            else
            {
                this.cloningChannel = channel;
                await this.beam.ConnectAsync(
                    this.config.BeamChannel,
                    this.config.BeamUsername,
                    this.config.BeamPassword);
                await channel.SendMessage("Beam chat cloning **ON**");
            }
        }

        private async Task TimeCommand(Channel channel)
        {
            var garyTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Mountain Standard Time").ToShortTimeString();
            var snorTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Central European Standard Time").ToShortTimeString();
            var message = $"It is {garyTime} glorious Gary time & {snorTime} upside-down euro time.";
            await channel.SendMessage(message);
        }

        private async Task VideoCommand(Channel channel, string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                await channel.SendMessage(this.wit.GetRequiredParamMessage());
                return;
            }

            await channel.SendIsTyping();

            try
            {
                var uri = $"https://www.googleapis.com/youtube/v3/search?&key={this.config.GoogleKey}&part=id&maxResults=1&q={query}&type=video";
                var request = WebRequest.CreateHttp(uri);
                using (var response = request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var text = await reader.ReadToEndAsync();
                    var json = JObject.Parse(text);
                    if (json != null)
                    {
                        var token = json.SelectToken($"items[0].id.videoId");
                        if (token != null)
                        {
                            var videoId = token.Value<string>();
                            var link = $"https://www.youtube.com/watch?v={videoId}";
                            await channel.SendMessage(link);
                        }
                    }
                }
            }
            catch (Exception)
            {
                await channel.SendMessage("Sorry bro, all out of videos.");
            }
        }

        private async Task ImageCommand(Channel channel, string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                await channel.SendMessage(this.wit.GetRequiredParamMessage());
                return;
            }

            await channel.SendIsTyping();

            try
            {
                int offset = this.random.Next(0, 45);
                var uri = $"https://www.googleapis.com/customsearch/v1?q={query}&num=1&start={offset}&imgSize=medium&searchType=image&key={this.config.GoogleKey}&cx={this.config.GoogleSearchCx}";
                var request = WebRequest.CreateHttp(uri);
                using (var response = request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var text = await reader.ReadToEndAsync();
                    var json = JObject.Parse(text);
                    if (json != null)
                    {
                        var token = json.SelectToken("items[0].link");
                        if (token != null)
                        {
                            var link = token.Value<string>();
                            await channel.SendMessage(link);
                        }
                    }
                }
            }
            catch (Exception)
            {
                await channel.SendMessage("Sorry bro, fresh out of quality images for today.");
            }
        }

        private async Task GifCommand(Channel channel, string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                await channel.SendMessage(this.wit.GetRequiredParamMessage());
                return;
            }

            try
            {
                int offset = this.random.Next(0, 45);
                var uri = $"https://api.giphy.com/v1/gifs/random?api_key={this.config.GiphyKey}&tag={query}";
                var request = WebRequest.CreateHttp(uri);
                using (var response = request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var text = await reader.ReadToEndAsync();
                    var json = JObject.Parse(text);
                    if (json != null)
                    {
                        var token = json.SelectToken("data.image_url");
                        if (token != null)
                        {
                            var link = token.Value<string>();
                            await channel.SendMessage(link);
                        }
                        else
                        {
                            await channel.SendMessage(this.wit.GetSpecificityMessage());
                        }
                    }
                }
            }
            catch (Exception)
            {
                await channel.SendMessage("What is a gif?");
            }
        }

        private async Task LyricsCommand(Channel channel, string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                await channel.SendMessage(this.wit.GetRequiredParamMessage());
                return;
            }

            await channel.SendIsTyping();

            try
            {
                string artistName = string.Empty;
                string trackName = string.Empty;
                string trackId = null;
                var uri = $"http://api.musixmatch.com/ws/1.1/track.search?apikey={this.config.MusixmatchKey}&f_has_lyrics&q={query}";
                var request = WebRequest.CreateHttp(uri);
                using (var response = request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var text = await reader.ReadToEndAsync();
                    var json = JObject.Parse(text);
                    if (json != null)
                    {
                        var token = json.SelectToken("message.body.track_list[0].track");
                        if (token != null)
                        {
                            trackId = token.SelectToken("track_id").Value<string>();
                            trackName = token.SelectToken("track_name").Value<string>();
                            artistName = token.SelectToken("artist_name").Value<string>();
                        }
                    }
                }

                if (trackId != null)
                {
                    uri = $"http://api.musixmatch.com/ws/1.1/track.lyrics.get?apikey={this.config.MusixmatchKey}&track_id={trackId}";
                    request = WebRequest.CreateHttp(uri);
                    using (var response = request.GetResponse())
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        var text = await reader.ReadToEndAsync();
                        var json = JObject.Parse(text);
                        if (json != null)
                        {
                            var token = json.SelectToken("message.body.lyrics.lyrics_body");
                            if (token != null)
                            {
                                var lyrics = token.Value<string>();
                                if (lyrics.EndsWith("******* This Lyrics is NOT for Commercial use *******"))
                                {
                                    lyrics = lyrics.Substring(0, lyrics.Length - 54);
                                }

                                var message = $"***{trackName}*** - {artistName}\r\n```{lyrics}```";
                                await channel.SendMessage(message);
                            }
                        }
                    }
                }
                else
                {
                    await channel.SendMessage("Don't know that one.");
                }
            }
            catch (Exception)
            {
                await channel.SendMessage("Sorry, have migraine, can't music.");
            }
        }

        private async Task CatCommand(Channel channel)
        {
            await channel.SendIsTyping();
            try
            {
                var uri = $"https://thecatapi.com/api/images/get?api_key={this.config.CatKey}&format=xml";
                var request = WebRequest.CreateHttp(uri);
                using (var response = request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var text = await reader.ReadToEndAsync();
                    var xml = XDocument.Parse(text);
                    var url = xml.Descendants("url").SingleOrDefault();
                    if (url != null)
                    {
                        await channel.SendMessage(url.Value);
                    }
                }
            }
            catch (Exception)
            {
                await channel.SendMessage("Cat overflow exception. Too many cats.");
            }
        }

        private async Task DefineCommand(Channel channel, string query)
        {
            await channel.SendIsTyping();
            try
            {
                var uri = $"https://mashape-community-urban-dictionary.p.mashape.com/define?term={query}";
                var request = WebRequest.CreateHttp(uri);
                request.Headers.Add("X-Mashape-Key", this.config.MashapeKey);
                using (var response = request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var text = await reader.ReadToEndAsync();
                    var json = JObject.Parse(text);
                    if (json != null)
                    {
                        var token = json.SelectToken("list[0].definition");
                        if (token != null)
                        {
                            var definition = token.Value<string>();
                            var word = json.SelectToken("list[0].word");
                            var example = json.SelectToken("list[0].example");
                            var message = $"\r\nDefinition of **{word}** ```\r\n{definition}```\r\n Example\r\n```\r\n{example}```";
                            await channel.SendMessage(message);
                        }
                        else
                        {
                            await channel.SendMessage("wat");
                        }
                    }
                }
            }
            catch (Exception)
            {
                await channel.SendMessage("I left my pocket dictionary at home.");
            }
        }

        private async Task ShitpostCommand(Channel channel)
        {
            await channel.SendIsTyping();
            try
            {
                var uri = $"https://api.imgur.com/3/g/memes/{this.random.Next(0, 100)}";
                var request = WebRequest.CreateHttp(uri);
                request.Headers.Add("Authorization", $"Client-ID {this.config.ImgurKey}");
                using (var response = request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var text = await reader.ReadToEndAsync();
                    var json = JObject.Parse(text);
                    if (json != null)
                    {
                        var token = json.SelectToken($"data[{this.random.Next(1, 55)}].link");
                        if (token != null)
                        {
                            var link = token.Value<string>();
                            await channel.SendMessage(link);
                        }
                    }
                }
            }
            catch (Exception)
            {
                await channel.SendMessage("Imgur is upset at you right now.");
            }
        }

        private async Task BullshitCommand(Channel channel)
        {
            await channel.SendIsTyping();
            try
            {
                var uri = "http://api.forismatic.com/api/1.0/?method=getQuote&format=text&lang=en";
                var request = WebRequest.CreateHttp(uri);
                using (var response = request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var text = await reader.ReadToEndAsync();
                    await channel.SendMessage(text);
                }
            }
            catch (Exception)
            {
                await channel.SendMessage("Sorry, guess the bullshit website is down.");
            }
        }

        private async Task RollCommand(Channel channel, User user)
        {
            int number = this.random.Next(0, 101);
            await channel.SendMessage($"{this.GetName(user)} rolled a {number}.");
        }

        private string GetName(User user)
        {
            if (string.IsNullOrEmpty(user.Nickname))
            {
                return user.Name;
            }
            else
            {
                return user.Nickname;
            }
        }
    }
}
