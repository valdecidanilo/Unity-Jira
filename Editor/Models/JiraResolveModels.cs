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
    internal sealed class JiraListPriority
    {
        public string id;
        public string name;
    }

    [Serializable]
    internal sealed class JiraSubtaskFields
    {
        public string summary;
        public JiraFullStatus status;
        public JiraListPriority priority;
        public JiraIssueType issuetype;
    }

    [Serializable]
    internal sealed class JiraSubtask
    {
        public string id;
        public string key;
        public JiraSubtaskFields fields;

        public string Summary => fields?.summary ?? string.Empty;
        public string StatusName => fields?.status?.name ?? string.Empty;
        public string StatusCategory => fields?.status?.CategoryKey ?? string.Empty;
    }

    [Serializable]
    internal sealed class JiraListFields
    {
        public string summary;
        public JiraFullStatus status;
        public JiraListPriority priority;
        public JiraIssueType issuetype;
        public JiraUser assignee;
        public string updated;
        public JiraSubtask[] subtasks;
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
        public string PriorityId => fields?.priority?.id ?? string.Empty;
        public string PriorityName => fields?.priority?.name ?? string.Empty;
        public JiraSubtask[] Subtasks => fields?.subtasks ?? Array.Empty<JiraSubtask>();
        public int SubtaskCount => fields?.subtasks?.Length ?? 0;
    }

    [Serializable]
    internal sealed class JiraListSearchPage
    {
        public JiraListIssue[] issues;
        public string nextPageToken;
    }

    // --- Editable issue fields ---

    [Serializable]
    internal sealed class JiraAdfAttributes
    {
        public string id;
        public string text;
    }

    [Serializable]
    internal sealed class JiraAdfNode
    {
        public string type;
        public string text;
        public JiraAdfAttributes attrs;
        public JiraAdfNode[] content;
    }

    [Serializable]
    internal sealed class JiraIssueEditFields
    {
        public string summary;
        public JiraAdfNode description;
        public JiraListPriority priority;
        public JiraIssueType issuetype;
        public JiraSubtask[] subtasks;
    }

    [Serializable]
    internal sealed class JiraIssueEditResponse
    {
        public string key;
        public JiraIssueEditFields fields;
        public string weightValue;
        public string teamValue;
    }

    // --- Transitions (workflow) ---

    [Serializable]
    internal sealed class JiraTransitionStatus
    {
        public string id;
        public string name;
        public JiraStatusCategoryRef statusCategory;

        public string CategoryKey => statusCategory?.key ?? string.Empty;
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
