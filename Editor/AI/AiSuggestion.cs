using System;

namespace OxenteGames.JiraCommunication.AI
{
    [Serializable]
    internal sealed class AiSuggestion
    {
        public string title;
        public string description;
        public string priority;
    }

    // --- Anthropic Messages API response shapes (only the fields we read) ---

    [Serializable]
    internal sealed class ClaudeMessageResponse
    {
        public ClaudeContentBlock[] content;
        public string stop_reason;
    }

    [Serializable]
    internal sealed class ClaudeContentBlock
    {
        public string type;
        public string text;
    }

    // --- OpenAI Chat Completions response shapes ---

    [Serializable]
    internal sealed class OpenAiChatResponse
    {
        public OpenAiChoice[] choices;
    }

    [Serializable]
    internal sealed class OpenAiChoice
    {
        public OpenAiMessage message;
    }

    [Serializable]
    internal sealed class OpenAiMessage
    {
        public string content;
    }

    // --- Shared error envelope (both providers expose error.message) ---

    [Serializable]
    internal sealed class AiErrorEnvelope
    {
        public AiErrorBody error;
    }

    [Serializable]
    internal sealed class AiErrorBody
    {
        public string type;
        public string message;
    }
}
