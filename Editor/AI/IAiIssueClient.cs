using System.Collections.Generic;
using System.Threading.Tasks;

namespace OxenteGames.JiraCommunication.AI
{
    /// <summary>
    /// Drafts a Jira issue (title, description, priority) from a short description.
    /// Implemented per AI provider (Anthropic Claude, OpenAI ChatGPT, ...).
    /// </summary>
    internal interface IAiIssueClient
    {
        Task<AiSuggestion> SuggestIssueAsync(
            string userInput,
            string projectName,
            string issueTypeName,
            IReadOnlyList<string> priorityNames,
            bool portuguese);
    }
}
