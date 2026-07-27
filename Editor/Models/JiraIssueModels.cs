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
}
