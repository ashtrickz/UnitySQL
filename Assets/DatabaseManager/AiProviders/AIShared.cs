namespace DatabaseManager.AiProviders
{
    public static class AIShared
    {
        [System.Serializable]
        public class Message
        {
            public string role;
            public string content;
        }

        public class ChatCompletionResponse
        {
            public Choice[] choices;
        }

        public class Choice
        {
            public Message message;
        }
    }
}
