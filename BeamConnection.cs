namespace DiscordButt.Beam
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    public class BeamConnection
    {
        private readonly JsonWebSocket socket;
        private readonly BeamPacketFactory packetFactory;
        private int autoIncId = 0;

        public BeamConnection()
        {
            this.socket = new JsonWebSocket();
            this.socket.OnMessageReceived += this.MessageReceived;
            this.packetFactory = new BeamPacketFactory();
            this.IsConnected = false;
        }

        public delegate void ChatMessage(string username, string message);

        public event ChatMessage OnChatMessage = (x, y) => { };

        public bool IsConnected { get; private set; }

        public async Task ConnectAsync(string channel, string username, string password)
        {
            int channelId, userId = 1;
            string endpoint, authkey;
            using (var handler = new HttpClientHandler())
            {
                handler.AllowAutoRedirect = true;
                handler.UseCookies = true;
                handler.CookieContainer = new CookieContainer();

                using (var client = new HttpClient(handler))
                {
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.BaseAddress = new Uri("https://beam.pro/api/v1/");
                    var credentials = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("username", username),
                        new KeyValuePair<string, string>("password", password)
                    });

                    // send credentials; get session cookie, csrf token, and userId
                    using (var response = await client.PostAsync("users/login", credentials))
                    {
                        var token = response.Headers.GetValues("X-CSRF-Token").FirstOrDefault();
                        client.DefaultRequestHeaders.Add("X-CSRF-Token", token);
                        var responseString = await response.Content.ReadAsStringAsync();
                        var responseJson = JObject.Parse(responseString);
                        userId = responseJson.SelectToken("channel.userId").Value<int>();
                    }

                    // get information about the channel we want to join
                    channelId = await client.GetJsonAsync<int>($"channels/{channel}?fields=id", "id");
                    var json = await client.GetJsonAsync($"chats/{channelId}");
                    endpoint = json.SelectToken("endpoints[0]").Value<string>();
                    authkey = json.Value<string>("authkey");
                }
            }

            // connect to the chat
            await this.socket.ConnectAsync(new Uri(endpoint));
            var packet = this.packetFactory.BuildAuthPacket(channelId, userId, authkey);
            await this.socket.SendAsync(packet);
            this.IsConnected = true;
        }

        public async Task Disconnect()
        {
            this.IsConnected = false;
            await this.socket.DisconnectAsync();
        }

        public async Task SendMessage(string message)
        {
            try
            {
                var packet = this.packetFactory.BuildChatPacket(message);
                await this.socket.SendAsync(packet);
            }
            catch (TaskCanceledException)
            {
                // ignore
            }
        }

        private void MessageReceived(JObject json)
        {
            try
            {
                var token = json.SelectToken("event");
                if (token != null && token.Value<string>() == "ChatMessage")
                {
                    var token1 = json.SelectToken("data.user_name");
                    var token2 = json.SelectToken("data.message.message[0].text");
                    if (token1 != null & token2 != null)
                    {
                        var username = token1.Value<string>();
                        var message = token2.Value<string>();
                        this.OnChatMessage(username, message);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // ignore
            }
        }

        private async Task SendAsync(string method, string[] arguments)
        {
            try
            {
                var message = new
                {
                    type = "method",
                    method = method,
                    arguments = arguments,
                    id = ++this.autoIncId
                };
                await this.socket.SendAsync(message);
            }
            catch (TaskCanceledException)
            {
                // ignore
            }
        }
    }
}
