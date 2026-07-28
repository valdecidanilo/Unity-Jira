using System;
using UnityEngine;

namespace OxenteGames.JiraCommunication.Models
{
    [Serializable]
    internal sealed class JiraProject
    {
        public string id;
        public string key;
        public string name;
    }

    [Serializable]
    internal sealed class JiraIssueType
    {
        public string id;
        public string name;
        public string description;
        public bool subtask;
        public int hierarchyLevel;
    }

    [Serializable]
    internal sealed class JiraBoard
    {
        public int id;
        public string name;
        public string type;
    }

    [Serializable]
    internal sealed class JiraSprint
    {
        public int id;
        public string name;
        public string state;
    }

    [Serializable]
    internal sealed class JiraEpic
    {
        public int id;
        public string key;
        public string name;
        public string summary;
        public bool done;

        public string DisplayName =>
            !string.IsNullOrWhiteSpace(name) ? name :
            !string.IsNullOrWhiteSpace(summary) ? summary : key;
    }

    // --- REST envelopes (JsonUtility cannot parse top-level arrays) ---

    [Serializable]
    internal sealed class JiraProjectPage
    {
        public JiraProject[] values;
    }

    [Serializable]
    internal sealed class JiraIssueTypePage
    {
        public JiraIssueType[] issueTypes;
        public JiraIssueType[] values;
    }

    [Serializable]
    internal sealed class JiraBoardPage
    {
        public JiraBoard[] values;
    }

    [Serializable]
    internal sealed class JiraSprintPage
    {
        public JiraSprint[] values;
        public int startAt;
        public int maxResults;
        public int total;
        public bool isLast;
    }

    [Serializable]
    internal sealed class JiraEpicPage
    {
        public JiraEpic[] values;
    }

    [Serializable]
    internal sealed class JiraEpicSearchFields
    {
        public string summary;
    }

    [Serializable]
    internal sealed class JiraEpicSearchIssue
    {
        public string id;
        public string key;
        public JiraEpicSearchFields fields;
    }

    [Serializable]
    internal sealed class JiraEpicSearchPage
    {
        public JiraEpicSearchIssue[] issues;
        public string nextPageToken;
        public bool isLast;
    }

    // Legacy fallback: GET /rest/api/3/issue/createmeta?projectKeys=X&expand=projects.issuetypes
    [Serializable]
    internal sealed class JiraClassicCreateMeta
    {
        public JiraClassicMetaProject[] projects;
    }

    [Serializable]
    internal sealed class JiraClassicMetaProject
    {
        public string key;
        public JiraIssueType[] issuetypes;
    }

    [Serializable]
    internal sealed class JiraCreatedIssue
    {
        public string id;
        public string key;
        public string self;
    }

    [Serializable]
    internal sealed class JiraAttachmentInfo
    {
        public string id;
        public string filename;
        public string content;
        public string mimeType;
        public string thumbnail;

        public bool IsImage =>
            !string.IsNullOrWhiteSpace(mimeType) &&
            mimeType.StartsWith(
                "image/",
                StringComparison.OrdinalIgnoreCase);
    }

    [Serializable]
    internal sealed class JiraAttachmentInfoList
    {
        public JiraAttachmentInfo[] items;
    }

    internal sealed class JiraAttachmentUploadResult
    {
        public JiraAttachmentInfo Attachment;
        public string Error;

        public bool Success => string.IsNullOrWhiteSpace(Error);
    }

    [Serializable]
    internal sealed class JiraErrorPayload
    {
        public string[] errorMessages;
    }

    // --- Create metadata fields (GET .../issuetypes/{id}) ---

    [Serializable]
    internal sealed class JiraFieldSchema
    {
        public string type;   // string, date, datetime, priority, user, option, array...
        public string items;  // element type when type == "array"
        public string system; // e.g. "summary", "duedate", "assignee", "priority"
        public string custom; // custom field type url (null for system fields)
        public int customId;
    }

    [Serializable]
    internal sealed class JiraAllowedValue
    {
        public string id;
        public string key;
        public string name;   // priority / version / component
        public string value;  // option / select
        public string accountId;
        public string displayName;
        public string title;  // Atlassian Team and other rich option values

        public string Display =>
            !string.IsNullOrWhiteSpace(displayName) ? displayName :
            !string.IsNullOrWhiteSpace(title) ? title :
            !string.IsNullOrWhiteSpace(value) ? value :
            !string.IsNullOrWhiteSpace(name) ? name :
            !string.IsNullOrWhiteSpace(key) ? key :
            !string.IsNullOrWhiteSpace(accountId) ? accountId : id;
    }

    [Serializable]
    internal sealed class JiraFieldMeta
    {
        public bool required;
        public string name;
        public string fieldId;
        public string description;
        public JiraFieldSchema schema;
        public JiraAllowedValue[] allowedValues;

        public bool HasAllowedValues => allowedValues != null && allowedValues.Length > 0;
    }

    [Serializable]
    internal sealed class JiraFieldMetaPage
    {
        public JiraFieldMeta[] fields;
        public JiraFieldMeta[] values;
        public int startAt;
        public int maxResults;
        public int total;
        public bool isLast;
    }

    [Serializable]
    internal sealed class JiraAllowedValuePage
    {
        public JiraAllowedValue[] values;
    }

    [Serializable]
    internal sealed class JiraIssuePickerResponse
    {
        public JiraIssuePickerSection[] sections;
    }

    [Serializable]
    internal sealed class JiraIssuePickerSection
    {
        public JiraIssuePickerIssue[] issues;
    }

    [Serializable]
    internal sealed class JiraIssuePickerIssue
    {
        public long id;
        public string key;
        public string summary;
        public string summaryText;

        public string DisplaySummary =>
            !string.IsNullOrWhiteSpace(summaryText)
                ? summaryText
                : summary;
    }

    // Wrapper so JsonUtility can read the top-level array returned by
    // GET /rest/api/3/user/assignable/search.
    [Serializable]
    internal sealed class JiraUserList
    {
        public JiraUser[] items;
    }

    [Serializable]
    internal sealed class JiraUserPickerResponse
    {
        public JiraUser[] users;
    }

    // --- Epic progress (child issues by status) ---

    [Serializable]
    internal sealed class JiraStatusCategoryRef
    {
        public string key;   // "new" | "indeterminate" | "done"
        public string name;
        public string colorName;
    }

    [Serializable]
    internal sealed class JiraWorkflowStatus
    {
        public string id;
        public string name;
        public JiraStatusCategoryRef statusCategory;
    }

    // Wrapper so JsonUtility can read the top-level array returned by
    // GET /rest/api/3/status.
    [Serializable]
    internal sealed class JiraWorkflowStatusList
    {
        public JiraWorkflowStatus[] items;
    }

    [Serializable]
    internal sealed class JiraIssueStatus
    {
        public JiraStatusCategoryRef statusCategory;
    }

    [Serializable]
    internal sealed class JiraChildIssueFields
    {
        public JiraIssueStatus status;
    }

    [Serializable]
    internal sealed class JiraChildIssue
    {
        public string key;
        public JiraChildIssueFields fields;
    }

    [Serializable]
    internal sealed class JiraIssueSearchPage
    {
        public JiraChildIssue[] issues;
        public int total;
    }

    internal sealed class JiraEpicProgress
    {
        public int Done { get; }
        public int Total { get; }
        public int Percent => Total > 0 ? Mathf.RoundToInt(Done * 100f / Total) : 0;

        public JiraEpicProgress(int done, int total)
        {
            Done = done;
            Total = total;
        }
    }
}
