using System;
using System.Globalization;
using System.Text;
using OxenteGames.JiraCommunication.Models;

namespace OxenteGames.JiraCommunication.Git
{
    /// <summary>
    /// Pure helpers that turn a Jira issue into a Conventional-Commits branch name and
    /// commit message. Keeping these side-effect free makes the naming convention easy
    /// to review and predict.
    /// </summary>
    internal static class GitConventions
    {
        /// <summary>Conventional Commits types offered in the UI.</summary>
        public static readonly string[] Types =
        {
            "feat", "fix", "chore", "docs", "refactor", "test", "perf", "build", "ci", "style"
        };

        public const string DefaultBranchTemplate = "{type}/{key}-{slug}";
        public const string DefaultCommitTemplate = "{type}({key}): {title}";

        /// <summary>
        /// Suggests a Conventional type from the Jira issue type. Bugs map to <c>fix</c>;
        /// everything else defaults to <c>feat</c>.
        /// </summary>
        public static string DefaultTypeFor(JiraIssueType issueType)
        {
            string name = issueType?.name?.Trim() ?? string.Empty;

            if (name.Length == 0)
                return "feat";

            if (EqualsAny(name, "Bug", "Defeito", "Erro"))
                return "fix";

            // História / Story / Tarefa / Task / Sub-task / Epic → feat by default.
            return "feat";
        }

        public static string BuildBranch(string template, string type, string key, string title)
        {
            string effective = string.IsNullOrWhiteSpace(template) ? DefaultBranchTemplate : template;
            return effective
                .Replace("{type}", type ?? string.Empty)
                .Replace("{key}", key ?? string.Empty)
                .Replace("{slug}", Slugify(title))
                .Replace("{title}", (title ?? string.Empty).Trim());
        }

        public static string BuildCommit(string template, string type, string key, string title)
        {
            string effective = string.IsNullOrWhiteSpace(template) ? DefaultCommitTemplate : template;
            return effective
                .Replace("{type}", type ?? string.Empty)
                .Replace("{key}", key ?? string.Empty)
                .Replace("{slug}", Slugify(title))
                .Replace("{title}", (title ?? string.Empty).Trim());
        }

        /// <summary>
        /// URL/branch-safe slug: lowercased, accents removed, non-alphanumerics collapsed to
        /// single hyphens, trimmed to <paramref name="maxLen"/> characters.
        /// </summary>
        public static string Slugify(string text, int maxLen = 50)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string normalized = text.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);
            bool lastWasHyphen = false;

            foreach (char c in normalized)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category == UnicodeCategory.NonSpacingMark)
                    continue; // drop accents

                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    builder.Append(c);
                    lastWasHyphen = false;
                }
                else if (c >= 'A' && c <= 'Z')
                {
                    builder.Append(char.ToLowerInvariant(c));
                    lastWasHyphen = false;
                }
                else if (!lastWasHyphen && builder.Length > 0)
                {
                    builder.Append('-');
                    lastWasHyphen = true;
                }
            }

            string slug = builder.ToString().Trim('-');
            if (slug.Length > maxLen)
                slug = slug.Substring(0, maxLen).Trim('-');

            return slug;
        }

        private static bool EqualsAny(string value, params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                if (string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
