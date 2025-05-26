using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using Unity.EditorCoroutines.Editor;
using UnityEngine.Networking;

namespace Nhance.UnityDatabaseTool.AiProviders
{
    public class NvidiaAIProvider : IAIProvider
    {
        private readonly string apiKey;
        private readonly string endpoint;

        public string Name => "NVIDIA (Nemotron)";

        public NvidiaAIProvider(string apiKey = "", string endpoint = "http://localhost:11434/v1/chat/completions")
        {
            this.apiKey = apiKey;
            this.endpoint = endpoint;
        }

        public void SendPrompt(string prompt, string systemPrompt, Action<string> onResponse)
        {
            EditorCoroutineUtility.StartCoroutineOwnerless(SendCoroutine(prompt, systemPrompt, onResponse));
        }

        private IEnumerator SendCoroutine(string prompt, string systemPrompt, Action<string> onResponse)
        {
            var messages = new[]
            {
                new AIShared.Message { role = "system", content = systemPrompt },
                new AIShared.Message { role = "user", content = prompt }
            };

            var payload = new
            {
                model = "nemotron", // или любой другой alias
                messages,
                temperature = 0.5
            };

            string json = JsonConvert.SerializeObject(payload);

            using (var request = new UnityWebRequest(endpoint, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(apiKey))
                    request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                    onResponse?.Invoke("Error: " + request.error);
                else
                {
                    var responseJson = request.downloadHandler.text;
                    var parsed = JsonConvert.DeserializeObject<AIShared.ChatCompletionResponse>(responseJson);
                    onResponse?.Invoke(parsed?.choices?[0]?.message?.content?.Trim() ?? "No response");
                }
            }
        }
    }
}

