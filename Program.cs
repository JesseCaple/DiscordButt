namespace DiscordButt
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Native.SetThreadExecutionState(EXECUTION_STATE.ES_DISPLAY_REQUIRED); // prevent server from sleeping
            var bot = new DiscordBot();
            bot.Start();
        }
    }
}
