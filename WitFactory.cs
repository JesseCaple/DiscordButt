namespace DiscordButt
{
    using System;

    public class WitFactory
    {
        private readonly Random random;
        private readonly string[] specificity;
        private readonly string[] requiredParam;

        public WitFactory()
        {
            this.random = new Random();

            this.specificity = new string[]
            {
                "Be less specific.",
                "That's way too precise.",
                "Come on, I can't find that.",
                "That is way too specific.",
            };

            this.requiredParam = new string[]
            {
                "You forgot a required parameter broseph.",
                "That command requires a param. Dummy.",
                "Did you forget the required parameter?",
                "Needs a param. How about use .help next time?",
                "One more parameter, bro.",
                "Hey, I think you missed a param there dumb-dumb.",
                "How about you type the required param.",
                "No required param, no command for you."
            };
        }

        public string GetSpecificityMessage()
        {
            return this.GetWit(this.specificity);
        }

        internal string GetRequiredParamMessage()
        {
            return this.GetWit(this.requiredParam);
        }

        private string GetWit(string[] array)
        {
            int index = this.random.Next(0, array.Length);
            return array[index];
        }
    }
}
