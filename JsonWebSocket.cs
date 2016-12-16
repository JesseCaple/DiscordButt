namespace DiscordButt
{
    using System;
    using System.IO;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// A WebSocket connection that can send and receive JSON messages.
    /// </summary>
    public class JsonWebSocket : IDisposable
    {
        private const int BUFFERSIZE = 4096;

        private ClientWebSocket socket;
        private CancellationTokenSource cancellation = null;
        private Task task = null;

        /// <summary>
        /// Event handler for receiving messages.
        /// </summary>
        /// <param name="message">The message received.</param>
        public delegate void ReceivedMessage(JObject message);

        /// <summary>
        /// Raised when a new message is received.
        /// </summary>
        public event ReceivedMessage OnMessageReceived =
            x => { };

        /// <summary>
        /// Frees unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.DisconnectAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Connects to an endpoint and starts background work.
        /// </summary>
        /// <param name="endpoint">The remote address to connect to.</param>
        /// <returns>>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task ConnectAsync(Uri endpoint)
        {
            if (this.task == null)
            {
                this.cancellation = new CancellationTokenSource();
                this.socket = new ClientWebSocket();
                await this.socket.ConnectAsync(endpoint, this.cancellation.Token);
                this.task = this.ListenAsync(this.cancellation.Token);
                var faf1 = this.task.ConfigureAwait(false);
                var faf2 = this.task.ContinueWith(
                    async task =>
                    {
                        Console.Error.WriteLine(task.Exception.InnerException.Message);
                        await this.DisconnectAsync();
                    },
                    TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        /// <summary>
        /// Disconnects the WebSocket and stops background work.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task DisconnectAsync()
        {
            if (this.task != null)
            {
                this.cancellation.Cancel();
                await this.task;
                await this.socket.CloseAsync(WebSocketCloseStatus.Empty, string.Empty, CancellationToken.None);
                this.socket.Dispose();
                this.cancellation.Dispose();
                this.task.Dispose();
                this.task = null;
            }
        }

        /// <summary>
        /// Sends the given object.
        /// </summary>
        /// <param name="obj">The object to serialize to Json.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task SendAsync(object obj)
        {
            try
            {
                var json = JObject.FromObject(obj);
                var str = json.ToString(Formatting.None);
                var binary = Encoding.UTF8.GetBytes(str);
                for (int offset = 0; offset < binary.Length; offset += BUFFERSIZE)
                {
                    var count = binary.Length - offset;
                    count = count > BUFFERSIZE ? BUFFERSIZE : count;
                    await this.socket.SendAsync(
                        new ArraySegment<byte>(binary, offset, count),
                        WebSocketMessageType.Text,
                        offset + count == binary.Length,
                        this.cancellation.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
        }

        /// <summary>
        /// Listens for incoming messages until canceled.
        /// </summary>
        /// <param name="token">The cancellation token to use.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task ListenAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var buffer = new ArraySegment<byte>(new byte[BUFFERSIZE]);
                    using (var stream = new MemoryStream())
                    {
                        WebSocketReceiveResult recResult;
                        do
                        {
                            recResult = await this.socket.ReceiveAsync(buffer, token);
                            stream.Write(buffer.Array, buffer.Offset, recResult.Count);
                            token.ThrowIfCancellationRequested();
                        }
                        while (!recResult.EndOfMessage);

                        stream.Seek(0, SeekOrigin.Begin);
                        var binary = stream.ToArray();
                        var str = Encoding.UTF8.GetString(binary, 0, binary.Length);
                        var json = JObject.Parse(str);
                        this.OnMessageReceived(json);
                    }
                }
                catch (OperationCanceledException)
                {
                    // ignored
                }
            }
        }
    }
}
