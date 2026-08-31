using System;
using System.Text;

namespace OxenteGames.JiraCommunication.Agents
{
    /// <summary>
    /// Builds the instructions that hand a task over to a local <c>ai-jira</c> install.
    /// </summary>
    /// <remarks>
    /// The sibling of <see cref="AgentPrompt"/>, and deliberately thinner. Where that
    /// one describes a task from scratch, this one only routes: the real instructions
    /// live in the <c>SKILL.md</c> that ships with ai-jira, and restating them here
    /// would create a second copy that drifts the first time that project changes.
    /// So every prompt names the skill and says what the developer wants, and nothing
    /// more.
    /// <para>
    /// One thing is added that ai-jira cannot know: a run started from the Unity
    /// window is headless, so the confirmation step ai-jira performs before touching
    /// Jira has no terminal to prompt in. The prompts tell the agent to stop and ask
    /// instead — its message lands in the chat, and the developer's answer continues
    /// the same session on the next turn, which is the same pause, in a different
    /// surface.
    /// </para>
    /// </remarks>
    internal static class AiJiraPrompt
    {
        /// <summary>The instruction for one ai-jira command, plus anything the user typed.</summary>
        public static string Build(string command, string userInstruction, bool portuguese)
        {
            var sb = new StringBuilder(768);

            sb.AppendLine(portuguese
                ? "Você está no repositório de um projeto Unity, aberto no Unity Editor."
                : "You are in a Unity project's repository, opened in the Unity Editor.");
            sb.AppendLine();

            sb.AppendLine(Intent(command, portuguese));
            sb.AppendLine();

            sb.Append(portuguese
                    ? "Use a skill `"
                    : "Use the `")
              .Append(command)
              .AppendLine(portuguese
                  ? "` do ai-jira para isso. Ela é a fonte da verdade do fluxo: siga o que ela manda em vez de chamar a API do Jira por conta própria."
                  : "` skill from ai-jira for this. It is the source of truth for the workflow: follow it instead of calling the Jira API yourself.");

            if (!string.IsNullOrWhiteSpace(userInstruction))
            {
                sb.AppendLine();
                sb.AppendLine(portuguese ? "Contexto que eu adicionei:" : "Context I added:");
                sb.AppendLine(userInstruction.Trim());
            }

            sb.AppendLine();
            sb.AppendLine(portuguese
                ? "Esta execução é headless: não há terminal para você perguntar nada no meio do caminho. "
                  + "Quando a skill pedir uma confirmação ou uma escolha — épico, tipo, time, prioridade —, "
                  + "pare e pergunte na sua resposta final. Ela aparece no chat da janela do Jira dentro do "
                  + "Unity e eu respondo no próximo turno, na mesma sessão."
                : "This run is headless: there is no terminal for you to prompt in mid-task. When the skill "
                  + "asks for a confirmation or a choice — epic, type, team, priority — stop and ask in your "
                  + "final message. It shows up in the Jira window's chat inside Unity and I answer on the "
                  + "next turn, in the same session.");

            return sb.ToString();
        }

        private static string Intent(string command, bool portuguese)
        {
            switch (command)
            {
                case AiJiraLocator.CommandCard:
                    return portuguese
                        ? "Crie o card no Jira a partir do que mudou no repositório, e faça o checkout do branch do card."
                        : "Create the Jira card from what changed in the repository, and check out the card's branch.";

                case AiJiraLocator.CommandPr:
                    return portuguese
                        ? "Cuide do pull request deste branch — abrir, ou avançar o que já existe — e sincronize o card depois."
                        : "Handle this branch's pull request — open it, or move the existing one along — and sync the card afterwards.";

                case AiJiraLocator.CommandSync:
                    return portuguese
                        ? "Sincronize o status dos cards com o estado real dos pull requests."
                        : "Sync the cards' status with the real state of the pull requests.";

                case AiJiraLocator.CommandInit:
                    return portuguese
                        ? "Configure o ai-jira nesta máquina: valide as credenciais e gere o config.json a partir da instância do Jira."
                        : "Set up ai-jira on this machine: validate the credentials and generate config.json from the Jira instance.";

                default:
                    return portuguese
                        ? "Execute o comando do ai-jira pedido abaixo."
                        : "Run the ai-jira command asked for below.";
            }
        }

        /// <summary>A short label for the run list and the chat bubble.</summary>
        public static string Title(string command)
        {
            return string.IsNullOrWhiteSpace(command) ? "ai-jira" : command;
        }

        /// <summary>
        /// Reads a <c>/jira-card ...</c> line typed into the chat composer.
        /// </summary>
        /// <remarks>
        /// The slash exists because that is how these commands are spelled everywhere
        /// else — in the CLI, in the README, in the developer's head. It is not the
        /// CLI's own slash syntax, though: a headless run has no interactive prompt to
        /// receive one, so what a slash reaches here is a local rewrite into the same
        /// routing prompt the panel builds. The developer never has to know that.
        /// <para>
        /// The <c>jira-</c> prefix is optional, so <c>/card</c> and <c>/jira-card</c>
        /// both land. Anything else returns false and is reported rather than sent:
        /// a mistyped command that silently becomes the first line of a prompt is a
        /// turn spent watching the agent puzzle over it.
        /// </para>
        /// </remarks>
        public static bool TryParseCommand(string text, out string command, out string rest)
        {
            command = string.Empty;
            rest = string.Empty;

            string trimmed = (text ?? string.Empty).TrimStart();
            if (!trimmed.StartsWith("/", StringComparison.Ordinal))
                return false;

            trimmed = trimmed.Substring(1);

            int split = trimmed.IndexOfAny(new[] { ' ', '\t', '\n', '\r' });
            string name = split < 0 ? trimmed : trimmed.Substring(0, split);
            rest = split < 0 ? string.Empty : trimmed.Substring(split).Trim();

            name = name.Trim().ToLowerInvariant();
            if (name.Length == 0)
                return false;

            if (!name.StartsWith("jira-", StringComparison.Ordinal))
                name = "jira-" + name;

            foreach (string known in AiJiraLocator.KnownCommands)
            {
                if (string.Equals(known, name, StringComparison.Ordinal))
                {
                    command = known;
                    return true;
                }
            }

            // Both outputs are cleared on the way out. A caller that reads `rest`
            // after a false return gets nothing rather than the tail of a path that
            // merely started with a slash.
            rest = string.Empty;
            return false;
        }

        /// <summary>True when the text looks like a slash command, valid or not.</summary>
        public static bool LooksLikeCommand(string text)
        {
            string trimmed = (text ?? string.Empty).TrimStart();
            return trimmed.Length > 1 && trimmed[0] == '/';
        }

        /// <summary>The commands as a single line, for the composer hint.</summary>
        public static string CommandList()
        {
            var sb = new StringBuilder(96);

            foreach (string command in AiJiraLocator.KnownCommands)
            {
                if (sb.Length > 0)
                    sb.Append("  ");

                sb.Append('/').Append(command);
            }

            return sb.ToString();
        }

        /// <summary>
        /// The section appended to the project's agent instructions when ai-jira is
        /// installed.
        /// </summary>
        /// <remarks>
        /// Its whole job is to settle a collision. Two Jira workflows are now in the
        /// agent's context — this package's own helper, and ai-jira's skills — and an
        /// agent handed both will pick one per turn, which is how the same developer
        /// gets a card created two different ways on two consecutive messages. The
        /// section states the precedence once, in the file the agent already reads.
        /// </remarks>
        public static void AppendSkillSection(StringBuilder sb, AiJiraInfo info)
        {
            sb.AppendLine("### ai-jira is installed on this machine");
            sb.AppendLine();
            sb.AppendLine("`ai-jira` is installed at `" + info.Home + "`. It carries a skill per command,");
            sb.AppendLine("each one reading this Jira instance's real project, field and status names from");
            sb.AppendLine("its own `config.json`. **Those skills take precedence over the `jira.sh` helper");
            sb.AppendLine("and over raw `curl`** — they know the workflow this team actually uses, and a");
            sb.AppendLine("hand-built API call does not.");
            sb.AppendLine();
            sb.AppendLine("| Want to | Use |");
            sb.AppendLine("| --- | --- |");
            sb.AppendLine("| Create a card from the current change | `jira-card` |");
            sb.AppendLine("| Open, review or merge the pull request | `jira-pr` |");
            sb.AppendLine("| Move cards to match the pull requests | `jira-sync` |");
            sb.AppendLine("| Re-read the Jira instance into `config.json` | `jira-init` |");
            sb.AppendLine();
            sb.AppendLine("Fall back to `jira.sh` or raw `curl` only for what no skill covers — reading an");
            sb.AppendLine("issue the prompt referenced, searching by title, leaving a comment.");
            sb.AppendLine();
            sb.AppendLine("That install is inside your workspace: the run is launched with `--add-dir` for");
            sb.AppendLine("it, so reading the skills, the scripts and `config.json` there is allowed. If a");
            sb.AppendLine("path under it is still refused, say which one — do not work around it by");
            sb.AppendLine("guessing the project, field or status names that file holds.");
            sb.AppendLine();

            if (!info.HasGh)
            {
                sb.AppendLine("`gh` is not on PATH here, so `jira-pr` cannot reach GitHub. Say so rather");
                sb.AppendLine("than working around it.");
                sb.AppendLine();
            }
        }
    }
}
