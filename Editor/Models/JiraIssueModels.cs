using System;

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
    }

    [Serializable]
    internal sealed class JiraEpicPage
    {
        public JiraEpic[] values;
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
        public string name;   // priority / version / component
        public string value;  // option / select

        public string Display =>
            !string.IsNullOrWhiteSpace(value) ? value :
            !string.IsNullOrWhiteSpace(name) ? name : id;
    }

    [Serializable]
    internal sealed class JiraFieldMeta
    {
        public bool required;
        public string name;
        public string fieldId;
        public JiraFieldSchema schema;
        public JiraAllowedValue[] allowedValues;

        public bool HasAllowedValues => allowedValues != null && allowedValues.Length > 0;
    }

    [Serializable]
    internal sealed class JiraFieldMetaPage
    {
        public JiraFieldMeta[] values;
    }

    // Wrapper so JsonUtility can read the top-level array returned by
    // GET /rest/api/3/user/assignable/search.
    [Serializable]
    internal sealed class JiraUserList
    {
        public JiraUser[] items;
    }
}
