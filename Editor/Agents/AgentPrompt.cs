using System.Text;

namespace OxenteGames.JiraCommunication.Agents
{
    /// <summary>
    /// Builds the instruction handed to a local agent.
    /// </summary>
    /// <remarks>
    /// The sibling of <c>AiPrompt</c>, but with a different job. <c>AiPrompt</c> asks a
    /// model to return JSON fields; this one describes a task in a real repository and
    /// leaves the agent free to read, edit and run things. It deliberately does not
    /// dictate a response format: constraining an agent's output shape is what turns a
    /// capable agent back into an autocomplete.
    /// </remarks>
    internal static class AgentPrompt
    {
        /// <summary>Frames a Jira issue as a task in the current Unity project.</summary>
        public static string BuildIssueTask(
            string issueKey,
            string summary,
            string description,
            string userInstruction,
            string branchName,
            bool portuguese)
        {
            var sb = new StringBuilder(1024);

            sb.AppendLine(portuguese
                ? "Você está trabalhando em um projeto Unity, dentro do repositório atual."
                : "You are working on a Unity project, inside the current repository.");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(issueKey))
            {
                sb.Append(portuguese ? "Atividade do Jira: " : "Jira issue: ")
                  .AppendLine(issueKey);
            }

            if (!string.IsNullOrWhiteSpace(summary))
            {
                sb.Append(portuguese ? "Título: " : "Summary: ").AppendLine(summary.Trim());
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                sb.AppendLine();
                sb.AppendLine(portuguese ? "Descrição da atividade:" : "Issue description:");
                sb.AppendLine(description.Trim());
            }

            if (!string.IsNullOrWhiteSpace(branchName))
            {
                sb.AppendLine();
                sb.Append(portuguese ? "Branch sugerido pela convenção do time: " : "Branch suggested by the team convention: ")
                  .AppendLine(branchName);
            }

            if (!string.IsNullOrWhiteSpace(userInstruction))
            {
                sb.AppendLine();
                sb.AppendLine(portuguese ? "O que fazer:" : "What to do:");
                sb.AppendLine(userInstruction.Trim());
            }

            sb.AppendLine();
            sb.AppendLine(portuguese
                ? "Observações do projeto: os arquivos .meta do Unity acompanham cada asset, "
                  + "e prefabs e cenas são YAML — edite-os com cuidado. Não faça commit nem push "
                  + "sem que isso tenha sido pedido explicitamente."
                : "Project notes: Unity .meta files accompany each asset, and prefabs and scenes "
                  + "are YAML — edit them carefully. Do not commit or push unless explicitly asked.");

            return sb.ToString();
        }

        /// <summary>A free-form task with only the project framing added.</summary>
        public static string BuildFreeTask(string userInstruction, bool portuguese)
        {
            var sb = new StringBuilder(512);

            sb.AppendLine(portuguese
                ? "Você está trabalhando em um projeto Unity, dentro do repositório atual."
                : "You are working on a Unity project, inside the current repository.");
            sb.AppendLine();
            sb.AppendLine((userInstruction ?? string.Empty).Trim());

            return sb.ToString();
        }

        /// <summary>
        /// A follow-up turn in a session that is being resumed.
        /// </summary>
        /// <remarks>
        /// Deliberately bare: the resumed session already holds the project framing,
        /// the issue and everything the agent read. Repeating that here would pay for
        /// the same context twice and is the whole reason to resume instead of
        /// starting fresh.
        /// </remarks>
        public static string BuildFollowUp(string userInstruction)
        {
            return (userInstruction ?? string.Empty).Trim();
        }

        /// <summary>A short label for the run list, derived from the task.</summary>
        public static string BuildTitle(string issueKey, string userInstruction)
        {
            if (!string.IsNullOrWhiteSpace(issueKey))
                return issueKey.Trim();

            string text = (userInstruction ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
            if (text.Length == 0)
                return "task";

            return text.Length > 48 ? text.Substring(0, 48) + "..." : text;
        }
    }
}
