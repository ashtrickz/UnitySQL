using System;

namespace Nhance.USQL.AI
{
    public interface IAIProvider
    {
        string Name { get; }
        void SendPrompt(string prompt, string systemPrompt, Action<string> onResponse);
    }
}

