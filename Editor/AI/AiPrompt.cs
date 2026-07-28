using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OxenteGames.JiraCommunication.AI
{
    /// <summary>Shared prompt construction and response parsing across AI providers.</summary>
    internal static class AiPrompt
    {
        public static string BuildSystem(IReadOnlyList<string> priorityNames, bool portuguese)
        {
            string language = portuguese ? "Portuguese (Brazil)" : "English";
            string priorities = (priorityNames != null && priorityNames.Count > 0)
                ? string.Join(", ", priorityNames)
                : "Highest, High, Medium, Low, Lowest";

            return
                "You write clear Jira issues for a software team. " +
                "Given a short description, produce a concise title and a well-structured description. " +
                "Reply ONLY with a raw JSON object, no markdown and no code fences, with exactly these keys: " +
                "\"title\" (a short string), " +
                "\"description\" (a string; may contain multiple lines), " +
                "\"priority\" (a string, exactly one of: " + priorities + "). " +
                "Write the title and description in " + language + ".";
        }

        public static string BuildContext(string userInput, string projectName, string issueTypeName)
        {
            var context = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(projectName))
                context.Append("Project: ").Append(projectName).Append(". ");
            if (!string.IsNullOrWhiteSpace(issueTypeName))
                context.Append("Issue type: ").Append(issueTypeName).Append(". ");
            context.Append("Description from the user: ").Append(userInput ?? string.Empty);
            return context.ToString();
        }

        public static AiSuggestion Parse(string modelText)
        {
            if (string.IsNullOrWhiteSpace(modelText))
                throw new Exception("A IA não retornou texto.");

            string json = StripToJson(modelText);
            var suggestion = JsonUtility.FromJson<AiSuggestion>(json);

            if (suggestion == null ||
                (string.IsNullOrWhiteSpace(suggestion.title) && string.IsNullOrWhiteSpace(suggestion.description)))
            {
                throw new Exception("Não foi possível interpretar a resposta da IA.");
            }

            return suggestion;
        }

        // The model is asked for raw JSON, but strip code fences / surrounding prose defensively.
        private static string StripToJson(string text)
        {
            string trimmed = text.Trim();
            trimmed = trimmed.Replace("```json", string.Empty).Replace("```", string.Empty).Trim();

            int start = trimmed.IndexOf('{');
            int end = trimmed.LastIndexOf('}');
            if (start >= 0 && end > start)
                return trimmed.Substring(start, end - start + 1);

            return trimmed;
        }

        public static string ExtractErrorMessage(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return null;

            try
            {
                var envelope = JsonUtility.FromJson<AiErrorEnvelope>(body);
                return envelope?.error?.message;
            }
            catch
            {
                return null;
            }
        }
    }
}
