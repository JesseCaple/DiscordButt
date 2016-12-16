#pragma warning disable SA1300 // Element must begin with upper-case letter
namespace DiscordButt.Beam
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    public class BeamPacketFactory
    {
        private static int autoInc = 0;

        public BeamPacket BuildPacket(string method, object[] arguments)
        {
            return new BeamPacket()
            {
                type = "method",
                method = method,
                arguments = arguments,
                id = Interlocked.Increment(ref autoInc)
            };
        }

        public BeamPacket BuildAuthPacket(int channelId, int userId, string authKey)
        {
            return new BeamPacket()
            {
                type = "method",
                method = "auth",
                arguments = new object[]
                {
                    channelId,
                    userId,
                    authKey
                },
                id = Interlocked.Increment(ref autoInc)
            };
        }

        public BeamPacket BuildChatPacket(string message)
        {
            return new BeamPacket()
            {
                type = "method",
                method = "msg",
                arguments = new object[]
                {
                    message
                },
                id = Interlocked.Increment(ref autoInc)
            };
        }

        public class BeamPacket
        {
            public string type { get; set; }

            public string method { get; set; }

            public object[] arguments { get; set; }

            public int id { get; set; }
        }

        public class BeamResponse
        {
            public string type { get; set; }

            public string error { get; set; }

            public int id { get; set; }

            public object data { get; set; }
        }
    }
}
#pragma warning restore SA1300 // Element must begin with upper-case letter
