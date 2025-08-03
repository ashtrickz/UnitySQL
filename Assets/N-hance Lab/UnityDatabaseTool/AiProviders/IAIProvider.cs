using System;

namespace Nhance.UnityDatabaseTool.AiProviders
{
    public interface IAIProvider
    {
        string Name { get; }
        void SendPrompt(string prompt, string systemPrompt, Action<string> onResponse);
    }
}

