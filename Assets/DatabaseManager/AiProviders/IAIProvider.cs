using System;

namespace DatabaseManager.AiProviders
{
    public interface IAIProvider
    {
        string Name { get; }
        void SendPrompt(string prompt, string systemPrompt, Action<string> onResponse);
    }
}

