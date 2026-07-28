using System;

namespace OxenteGames.JiraCommunication.Models
{
    // --- Issue list (search results) ---

    [Serializable]
    internal sealed class JiraFullStatus
    {
        public string name;
        public JiraStatusCategoryRef statusCategory;

        public string CategoryKey => statusCategory?.key ?? string.Empty;
    }

    [Serializable]
    internal sealed class JiraListFields
    {
        public string summary;
        public JiraFullStatus status;
        public JiraUser assignee;
        public string updated;
    }

    [Serializable]
    internal sealed class JiraListIssue
    {
        public string id;
        public string key;
        public JiraListFields fields;

        public string Summary => fields?.summary ?? string.Empty;
        public string StatusName => fields?.status?.name ?? string.Empty;
        public string StatusCategory => fields?.status?.CategoryKey ?? string.Empty;
    }

    [Serializable]
    internal sealed class JiraListSearchPage
    {
        public JiraListIssue[] issues;
        public string nextPageToken;
    }

    // --- Transitions (workflow) ---

    [Serializable]
    internal sealed class JiraTransitionStatus
    {
        public string id;
        public string name;
    }

    [Serializable]
    internal sealed class JiraTransition
    {
        public string id;
        public string name;
        public JiraTransitionStatus to;

        public string TargetStatus => to?.name;
    }

    [Serializable]
    internal sealed class JiraTransitionList
    {
        public JiraTransition[] transitions;
    }
}
