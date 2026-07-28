using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using OxenteGames.JiraCommunication.Models;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace OxenteGames.JiraCommunication.API
{
    internal sealed class JiraClient
    {
        private readonly string _baseUrl;
        private readonly IJiraAuthProvider _authProvider;

        public JiraClient(string baseUrl, IJiraAuthProvider authProvider)
        {
            _baseUrl = NormalizeBaseUrl(baseUrl);
            _authProvider = authProvider ?? throw new ArgumentNullException(nameof(authProvider));
        }

        public string BaseUrl => _baseUrl;

        // --- Connection -----------------------------------------------------

        public async Task<JiraConnectionResult> TestConnectionAsync()
        {
            JiraResponse response = await SendAsync(UnityWebRequest.kHttpVerbGET, "/rest/api/3/myself", null);
            if (!response.Success)
                return JiraConnectionResult.Fail(response.StatusCode, response.Error);

            try
            {
                var user = JsonUtility.FromJson<JiraUser>(response.Body);
                return JiraConnectionResult.Ok(user);
            }
            catch (Exception exception)
            {
                return JiraConnectionResult.Fail(
                    response.StatusCode,
                    $"Não foi possível processar a resposta do Jira: {exception.Message}");
            }
        }

        public async Task<JiraUser> GetMyselfAsync()
        {
            JiraResponse response = await SendAsync(UnityWebRequest.kHttpVerbGET, "/rest/api/3/myself", null);
            if (!response.Success)
                return null;

            try { return JsonUtility.FromJson<JiraUser>(response.Body); }
            catch { return null; }
        }

        // --- Metadata -------------------------------------------------------

        public async Task<List<JiraProject>> GetProjectsAsync()
        {
            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbGET,
                "/rest/api/3/project/search?maxResults=100&orderBy=name",
                null);

            ThrowIfFailed(response, "Não foi possível carregar os projetos.");
            var page = JsonUtility.FromJson<JiraProjectPage>(response.Body);
            return ToList(page?.values);
        }

        public async Task<List<JiraIssueType>> GetIssueTypesAsync(string projectKey)
        {
            string encodedKey = UnityWebRequest.EscapeURL(projectKey);

            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbGET,
                $"/rest/api/3/issue/createmeta/{encodedKey}/issuetypes?maxResults=100",
                null);

            if (response.Success)
            {
                var page = JsonUtility.FromJson<JiraIssueTypePage>(response.Body);
                List<JiraIssueType> types =
                    ToList(page?.issueTypes ?? page?.values);
                if (types.Count > 0)
                    return types;
            }

            List<JiraIssueType> legacy = await GetIssueTypesLegacyAsync(encodedKey);
            if (legacy.Count > 0)
                return legacy;

            if (!response.Success)
                ThrowIfFailed(response, "Não foi possível carregar os tipos de issue do projeto.");

            return new List<JiraIssueType>();
        }

        private async Task<List<JiraIssueType>> GetIssueTypesLegacyAsync(string encodedProjectKey)
        {
            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbGET,
                $"/rest/api/3/issue/createmeta?projectKeys={encodedProjectKey}&expand=projects.issuetypes",
                null);

            if (!response.Success)
                return new List<JiraIssueType>();

            var meta = JsonUtility.FromJson<JiraClassicCreateMeta>(response.Body);
            if (meta?.projects != null && meta.projects.Length > 0)
                return ToList(meta.projects[0].issuetypes);

            return new List<JiraIssueType>();
        }

        /// <summary>Fields available when creating a given issue type in a project.</summary>
        public async Task<List<JiraFieldMeta>> GetCreateFieldsAsync(string projectKey, string issueTypeId)
        {
            var fields = new List<JiraFieldMeta>();
            string encodedProject = UnityWebRequest.EscapeURL(projectKey);
            string encodedType = UnityWebRequest.EscapeURL(issueTypeId);
            int startAt = 0;
            int pageCount = 0;

            while (pageCount < 20)
            {
                JiraResponse response = await SendAsync(
                    UnityWebRequest.kHttpVerbGET,
                    $"/rest/api/3/issue/createmeta/{encodedProject}/issuetypes/{encodedType}" +
                    $"?startAt={startAt}&maxResults=100",
                    null);

                ThrowIfFailed(
                    response,
                    "Não foi possível carregar os campos configurados para este tipo de issue.");

                var page = JsonUtility.FromJson<JiraFieldMetaPage>(response.Body);
                JiraFieldMeta[] pageFields = page?.fields ?? page?.values;
                if (pageFields == null || pageFields.Length == 0)
                    break;

                fields.AddRange(pageFields);
                startAt += pageFields.Length;
                pageCount++;

                if (page.isLast || (page.total > 0 && startAt >= page.total))
                    break;
            }

            return fields;
        }

        /// <summary>
        /// Fields currently visible and editable for an existing issue.
        /// Unlike create metadata, this also reflects the issue's edit screen.
        /// </summary>
        public async Task<List<JiraFieldMeta>> GetEditFieldsAsync(
            string issueKey)
        {
            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbGET,
                $"/rest/api/3/issue/{UnityWebRequest.EscapeURL(issueKey)}/editmeta",
                null);

            ThrowIfFailed(
                response,
                "Não foi possível carregar os campos editáveis da atividade.");
            return ParseFieldMap(response.Body);
        }

        public async Task<List<JiraAllowedValue>> GetPrioritiesAsync()
        {
            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbGET,
                "/rest/api/3/priority/search?maxResults=100",
                null);

            if (!response.Success)
                return new List<JiraAllowedValue>();

            var page = JsonUtility.FromJson<JiraAllowedValuePage>(response.Body);
            return ToList(page?.values);
        }

        public async Task<List<JiraIssuePickerIssue>>
            SearchIssuePickerAsync(
                string query,
                string currentProjectId)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<JiraIssuePickerIssue>();

            string path =
                "/rest/api/3/issue/picker?query=" +
                UnityWebRequest.EscapeURL(query.Trim());
            if (!string.IsNullOrWhiteSpace(currentProjectId))
            {
                path += "&currentProjectId=" +
                        UnityWebRequest.EscapeURL(currentProjectId);
            }

            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbGET,
                path,
                null);
            ThrowIfFailed(
                response,
                "Não foi possível pesquisar itens associados.");

            var picker = JsonUtility.FromJson<JiraIssuePickerResponse>(
                response.Body);
            var results = new List<JiraIssuePickerIssue>();
            var keys = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            if (picker?.sections == null)
                return results;

            foreach (JiraIssuePickerSection section in picker.sections)
            {
                if (section?.issues == null)
                    continue;

                foreach (JiraIssuePickerIssue issue in section.issues)
                {
                    if (issue == null ||
                        string.IsNullOrWhiteSpace(issue.key) ||
                        !keys.Add(issue.key))
                    {
                        continue;
                    }

                    results.Add(issue);
                    if (results.Count >= 12)
                        return results;
                }
            }

            return results;
        }

        public async Task<List<JiraWorkflowStatus>> GetStatusesAsync()
        {
            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbGET,
                "/rest/api/3/status",
                null);

            ThrowIfFailed(
                response,
                "Não foi possível sincronizar os status configurados no Jira.");

            if (string.IsNullOrWhiteSpace(response.Body))
                return new List<JiraWorkflowStatus>();

            var wrapped = JsonUtility.FromJson<JiraWorkflowStatusList>(
                "{\"items\":" + response.Body + "}");
            return ToList(wrapped?.items);
        }

        public async Task<List<JiraUser>> GetAssignableUsersAsync(string projectKey)
        {
            var users = new List<JiraUser>();
            var accountIds = new HashSet<string>();
            string encodedProject = UnityWebRequest.EscapeURL(projectKey);
            const int pageSize = 100;
            int startAt = 0;

            while (startAt < 2000)
            {
                JiraResponse response = await SendAsync(
                    UnityWebRequest.kHttpVerbGET,
                    $"/rest/api/3/user/assignable/search?project={encodedProject}" +
                    $"&startAt={startAt}&maxResults={pageSize}",
                    null);

                if (!response.Success || string.IsNullOrWhiteSpace(response.Body))
                    break;

                try
                {
                    // Body is a top-level JSON array; wrap it so JsonUtility can read it.
                    var wrapped = JsonUtility.FromJson<JiraUserList>(
                        "{\"items\":" + response.Body + "}");
                    JiraUser[] pageUsers = wrapped?.items;
                    if (pageUsers == null || pageUsers.Length == 0)
                        break;

                    int added = 0;
                    foreach (JiraUser user in pageUsers)
                    {
                        if (user == null ||
                            string.IsNullOrWhiteSpace(user.accountId) ||
                            !accountIds.Add(user.accountId))
                            continue;

                        users.Add(user);
                        added++;
                    }

                    startAt += pageUsers.Length;
                    if (added == 0)
                        break;
                }
                catch
                {
                    break;
                }
            }

            return users;
        }

        public async Task<List<JiraUser>> SearchAssignableUsersAsync(
            string projectKey,
            string query)
        {
            if (string.IsNullOrWhiteSpace(projectKey) ||
                string.IsNullOrWhiteSpace(query))
            {
                return new List<JiraUser>();
            }

            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbGET,
                "/rest/api/3/user/assignable/search" +
                "?project=" +
                UnityWebRequest.EscapeURL(projectKey) +
                "&query=" +
                UnityWebRequest.EscapeURL(query.Trim()) +
                "&startAt=0&maxResults=100",
                null);
            return ParseUserArray(response);
        }

        public async Task<List<JiraUser>> SearchUserPickerAsync(
            string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<JiraUser>();

            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbGET,
                "/rest/api/3/user/picker?query=" +
                UnityWebRequest.EscapeURL(query.Trim()) +
                "&maxResults=50&showAvatar=false" +
                "&excludeConnectUsers=true",
                null);
            if (!response.Success ||
                string.IsNullOrWhiteSpace(response.Body))
            {
                return new List<JiraUser>();
            }

            try
            {
                var picker =
                    JsonUtility.FromJson<JiraUserPickerResponse>(
                        response.Body);
                return ToList(picker?.users);
            }
            catch
            {
                return new List<JiraUser>();
            }
        }

        public async Task<List<JiraBoard>> GetBoardsAsync(string projectKey)
        {
            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbGET,
                $"/rest/agile/1.0/board?projectKeyOrId={UnityWebRequest.EscapeURL(projectKey)}&maxResults=50",
                null);

            if (!response.Success)
                return new List<JiraBoard>();

            var page = JsonUtility.FromJson<JiraBoardPage>(response.Body);
            return ToList(page?.values);
        }

        public async Task<List<JiraSprint>> GetAvailableSprintsAsync(
            int boardId)
        {
            var sprints = new List<JiraSprint>();
            var ids = new HashSet<int>();
            const int pageSize = 100;
            int startAt = 0;

            while (startAt < 2000)
            {
                JiraResponse response = await SendAsync(
                    UnityWebRequest.kHttpVerbGET,
                    $"/rest/agile/1.0/board/{boardId}/sprint" +
                    "?state=active,future" +
                    $"&startAt={startAt}&maxResults={pageSize}",
                    null);

                if (!response.Success)
                    break;

                var page = JsonUtility.FromJson<JiraSprintPage>(
                    response.Body);
                JiraSprint[] values = page?.values;
                if (values == null || values.Length == 0)
                    break;

                foreach (JiraSprint sprint in values)
                {
                    if (sprint != null && ids.Add(sprint.id))
                        sprints.Add(sprint);
                }

                startAt += values.Length;
                if (page.isLast ||
                    values.Length < pageSize ||
                    (page.total > 0 && startAt >= page.total))
                {
                    break;
                }
            }

            return sprints;
        }

        public async Task<List<JiraEpic>> GetEpicsAsync(int boardId)
        {
            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbGET,
                $"/rest/agile/1.0/board/{boardId}/epic?done=false&maxResults=50",
                null);

            if (!response.Success)
                return new List<JiraEpic>();

            var page = JsonUtility.FromJson<JiraEpicPage>(response.Body);
            return ToList(page?.values);
        }

        /// <summary>
        /// Returns every epic-level issue from the project, independently of board filters.
        /// Uses hierarchyLevel instead of the issue type name so renamed epics also work.
        /// </summary>
        public async Task<List<JiraEpic>> GetProjectEpicsAsync(string projectKey)
        {
            var epics = new List<JiraEpic>();
            string escapedKey = EscapeJqlString(projectKey);
            string jql = $"project = \"{escapedKey}\" AND hierarchyLevel = 1 ORDER BY updated DESC";
            string nextPageToken = null;
            int pageCount = 0;

            do
            {
                string path =
                    $"/rest/api/3/search/jql?jql={UnityWebRequest.EscapeURL(jql)}" +
                    "&fields=summary&maxResults=100";

                if (!string.IsNullOrWhiteSpace(nextPageToken))
                    path += $"&nextPageToken={UnityWebRequest.EscapeURL(nextPageToken)}";

                JiraResponse response = await SendAsync(
                    UnityWebRequest.kHttpVerbGET,
                    path,
                    null);

                ThrowIfFailed(response, "Não foi possível carregar os épicos do projeto.");

                var page = JsonUtility.FromJson<JiraEpicSearchPage>(response.Body);
                if (page?.issues == null)
                    break;

                foreach (JiraEpicSearchIssue issue in page.issues)
                {
                    if (issue == null || string.IsNullOrWhiteSpace(issue.key))
                        continue;

                    int.TryParse(issue.id, out int numericId);
                    epics.Add(new JiraEpic
                    {
                        id = numericId,
                        key = issue.key,
                        summary = issue.fields?.summary,
                        name = issue.fields?.summary,
                        done = false
                    });
                }

                nextPageToken = page.nextPageToken;
                pageCount++;
            }
            while (!string.IsNullOrWhiteSpace(nextPageToken) && pageCount < 20);

            return epics;
        }

        /// <summary>Completion of an epic based on its child issues (done vs total).</summary>
        public async Task<JiraEpicProgress> GetEpicProgressAsync(string epicKey)
        {
            string encoded = UnityWebRequest.EscapeURL(epicKey);

            // Agile API (works well on company-managed boards).
            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbGET,
                $"/rest/agile/1.0/epic/{encoded}/issue?fields=status&maxResults=200",
                null);

            JiraIssueSearchPage page = response.Success
                ? JsonUtility.FromJson<JiraIssueSearchPage>(response.Body)
                : null;

            // Fallback for team-managed projects: children link via the parent field.
            if (page?.issues == null || page.issues.Length == 0)
            {
                string jql = UnityWebRequest.EscapeURL(
                    $"parent = \"{EscapeJqlString(epicKey)}\"");
                JiraResponse search = await SendAsync(
                    UnityWebRequest.kHttpVerbGET,
                    $"/rest/api/3/search/jql?jql={jql}&fields=status&maxResults=200",
                    null);

                page = search.Success
                    ? JsonUtility.FromJson<JiraIssueSearchPage>(search.Body)
                    : null;
            }

            if (page?.issues == null)
                return new JiraEpicProgress(0, 0);

            int done = 0;
            foreach (JiraChildIssue issue in page.issues)
            {
                if (issue?.fields?.status?.statusCategory?.key == "done")
                    done++;
            }

            return new JiraEpicProgress(done, page.issues.Length);
        }

        // --- Mutations ------------------------------------------------------

        public async Task<JiraCreateIssueResult> CreateIssueAsync(JiraIssueDraft draft)
        {
            if (draft == null)
                return JiraCreateIssueResult.Fail("Rascunho de issue inválido.");

            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbPOST,
                "/rest/api/3/issue",
                draft.ToJson());

            if (!response.Success)
                return JiraCreateIssueResult.Fail(BuildIssueError(response));

            try
            {
                var created = JsonUtility.FromJson<JiraCreatedIssue>(response.Body);
                if (created == null || string.IsNullOrEmpty(created.key))
                    return JiraCreateIssueResult.Fail("O Jira respondeu, mas não retornou a chave da issue.");

                return JiraCreateIssueResult.Ok(created.id, created.key);
            }
            catch (Exception exception)
            {
                return JiraCreateIssueResult.Fail(
                    $"Issue possivelmente criada, mas a resposta não pôde ser lida: {exception.Message}");
            }
        }

        /// <summary>Moves an issue into a sprint. Returns null on success or an error message.</summary>
        public async Task<string> MoveIssueToSprintAsync(int sprintId, string issueKey)
        {
            string body = "{\"issues\":[\"" + issueKey + "\"]}";
            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbPOST,
                $"/rest/agile/1.0/sprint/{sprintId}/issue",
                body);

            return response.Success ? null : BuildIssueError(response);
        }

        /// <summary>Uploads a file to an issue. Returns null on success or an error message.</summary>
        public async Task<string> UploadAttachmentAsync(string issueKey, string filePath)
        {
            JiraAttachmentUploadResult result =
                await UploadAttachmentWithResultAsync(issueKey, filePath);
            return result.Error;
        }

        public async Task<JiraAttachmentUploadResult>
            UploadAttachmentWithResultAsync(
                string issueKey,
                string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return new JiraAttachmentUploadResult
                {
                    Error = "Arquivo de anexo não encontrado."
                };
            }

            byte[] bytes;
            try { bytes = File.ReadAllBytes(filePath); }
            catch (Exception exception)
            {
                return new JiraAttachmentUploadResult
                {
                    Error =
                        $"Não foi possível ler o anexo: {exception.Message}"
                };
            }

            string fileName = Path.GetFileName(filePath);
            var form = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", bytes, fileName, "application/octet-stream")
            };

            UnityWebRequest request = UnityWebRequest.Post(_baseUrl + $"/rest/api/3/issue/{issueKey}/attachments", form);
            request.timeout = 60;
            request.SetRequestHeader("X-Atlassian-Token", "no-check");

            JiraResponse response = await AwaitRequest(request);
            if (!response.Success)
            {
                return new JiraAttachmentUploadResult
                {
                    Error = BuildIssueError(response)
                };
            }

            JiraAttachmentInfo attachment = null;
            if (!string.IsNullOrWhiteSpace(response.Body))
            {
                string wrapped = "{\"items\":" + response.Body + "}";
                JiraAttachmentInfoList list =
                    JsonUtility.FromJson<JiraAttachmentInfoList>(wrapped);
                if (list?.items != null && list.items.Length > 0)
                    attachment = list.items[0];
            }

            return new JiraAttachmentUploadResult
            {
                Attachment = attachment
            };
        }

        // --- Resolve / conclude --------------------------------------------

        /// <summary>Runs a JQL search and returns the matching issues (summary + status).</summary>
        public async Task<List<JiraListIssue>> SearchIssuesAsync(string jql, int maxResults)
        {
            int totalLimit = Mathf.Clamp(maxResults, 1, 1000);
            int pageSize = Mathf.Min(100, totalLimit);
            string nextPageToken = null;
            var issues = new List<JiraListIssue>(Mathf.Min(totalLimit, 100));
            int pageCount = 0;

            do
            {
                string path =
                    $"/rest/api/3/search/jql?jql={UnityWebRequest.EscapeURL(jql)}" +
                    "&fields=summary,status,priority,issuetype,assignee,updated,subtasks" +
                    $"&maxResults={pageSize}";
                if (!string.IsNullOrWhiteSpace(nextPageToken))
                {
                    path += "&nextPageToken=" +
                            UnityWebRequest.EscapeURL(nextPageToken);
                }

                JiraResponse response = await SendAsync(
                    UnityWebRequest.kHttpVerbGET,
                    path,
                    null);

                ThrowIfFailed(response, "Não foi possível carregar as issues.");
                var page = JsonUtility.FromJson<JiraListSearchPage>(response.Body);
                if (page?.issues != null)
                {
                    foreach (JiraListIssue issue in page.issues)
                    {
                        if (issue != null)
                            issues.Add(issue);
                        if (issues.Count >= totalLimit)
                            break;
                    }
                }

                nextPageToken = page?.nextPageToken;
                pageCount++;
            }
            while (issues.Count < totalLimit &&
                   !string.IsNullOrWhiteSpace(nextPageToken) &&
                   pageCount < 20);

            return issues;
        }

        /// <summary>
        /// Loads the direct children of an issue. Jira uses the same parent
        /// relationship for Epic children and for subtasks.
        /// </summary>
        public Task<List<JiraListIssue>> GetDirectChildIssuesAsync(
            string parentKey,
            int maxResults = 200)
        {
            if (string.IsNullOrWhiteSpace(parentKey))
            {
                return Task.FromResult(new List<JiraListIssue>());
            }

            string jql =
                $"parent = \"{EscapeJqlString(parentKey.Trim())}\" " +
                "ORDER BY created ASC";
            return SearchIssuesAsync(jql, maxResults);
        }

        /// <summary>Loads the fields that can be edited from the Resolver panel.</summary>
        public async Task<JiraIssueEditResponse> GetIssueForEditAsync(
            string issueKey,
            string weightFieldId = null,
            string teamFieldId = null)
        {
            string fields = "summary,description,subtasks,priority,issuetype";
            if (!string.IsNullOrWhiteSpace(weightFieldId))
                fields += "," + weightFieldId;
            if (!string.IsNullOrWhiteSpace(teamFieldId) &&
                !string.Equals(
                    teamFieldId,
                    weightFieldId,
                    StringComparison.OrdinalIgnoreCase))
            {
                fields += "," + teamFieldId;
            }

            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbGET,
                $"/rest/api/3/issue/{UnityWebRequest.EscapeURL(issueKey)}" +
                "?fields=" + UnityWebRequest.EscapeURL(fields),
                null);

            ThrowIfFailed(response, "Não foi possível carregar os dados da atividade.");
            JiraIssueEditResponse issue =
                JsonUtility.FromJson<JiraIssueEditResponse>(response.Body);
            if (issue != null && !string.IsNullOrWhiteSpace(weightFieldId))
            {
                issue.weightValue = ExtractJsonPrimitive(
                    response.Body,
                    weightFieldId);
            }
            if (issue != null && !string.IsNullOrWhiteSpace(teamFieldId))
            {
                issue.teamValue = ExtractJsonFieldIdentifier(
                    response.Body,
                    teamFieldId);
            }
            return issue;
        }

        /// <summary>Updates summary and description. Returns null on success.</summary>
        public async Task<string> UpdateIssueAsync(
            string issueKey,
            string summary,
            string description,
            bool updateSummary,
            bool updateDescription)
        {
            var body = new StringBuilder(256);
            body.Append("{\"fields\":{");
            bool wroteField = false;
            if (updateSummary)
            {
                body.Append("\"summary\":\"")
                    .Append(JiraIssueDraft.JsonEscape(summary ?? string.Empty))
                    .Append('"');
                wroteField = true;
            }
            if (updateDescription)
            {
                if (wroteField)
                    body.Append(',');
                body.Append("\"description\":")
                    .Append(JiraAdf.BuildTextDocument(description ?? string.Empty));
            }
            body.Append("}}");

            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbPUT,
                $"/rest/api/3/issue/{UnityWebRequest.EscapeURL(issueKey)}",
                body.ToString());

            return response.Success ? null : BuildIssueError(response);
        }

        public async Task<string> UpdateIssueDescriptionAdfAsync(
            string issueKey,
            string descriptionAdf)
        {
            if (string.IsNullOrWhiteSpace(descriptionAdf))
                return "A descrição em ADF está vazia.";

            string body =
                "{\"fields\":{\"description\":" +
                descriptionAdf +
                "}}";
            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbPUT,
                $"/rest/api/3/issue/{UnityWebRequest.EscapeURL(issueKey)}",
                body);
            return response.Success ? null : BuildIssueError(response);
        }

        /// <summary>Changes only the issue priority. Returns null on success.</summary>
        public async Task<string> UpdateIssuePriorityAsync(string issueKey, string priorityId)
        {
            string body =
                "{\"fields\":{\"priority\":{\"id\":\"" +
                JiraIssueDraft.JsonEscape(priorityId ?? string.Empty) +
                "\"}}}";

            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbPUT,
                $"/rest/api/3/issue/{UnityWebRequest.EscapeURL(issueKey)}",
                body);

            return response.Success ? null : BuildIssueError(response);
        }

        public async Task<string> UpdateIssueNumberAsync(
            string issueKey,
            string fieldId,
            string invariantValue)
        {
            if (string.IsNullOrWhiteSpace(fieldId))
                return null;

            string value = string.IsNullOrWhiteSpace(invariantValue)
                ? "null"
                : invariantValue;
            string body =
                "{\"fields\":{\"" +
                JiraIssueDraft.JsonEscape(fieldId) +
                "\":" + value + "}}";

            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbPUT,
                $"/rest/api/3/issue/{UnityWebRequest.EscapeURL(issueKey)}",
                body);

            return response.Success ? null : BuildIssueError(response);
        }

        /// <summary>Available workflow transitions for an issue.</summary>
        public async Task<List<JiraTransition>> GetTransitionsAsync(string issueKey)
        {
            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbGET,
                $"/rest/api/3/issue/{UnityWebRequest.EscapeURL(issueKey)}/transitions",
                null);

            if (!response.Success)
                return new List<JiraTransition>();

            var list = JsonUtility.FromJson<JiraTransitionList>(response.Body);
            return ToList(list?.transitions);
        }

        /// <summary>
        /// Applies a workflow transition, optionally attaching a comment (ADF body).
        /// Returns null on success or a friendly error message.
        /// </summary>
        public async Task<string> ApplyTransitionAsync(string issueKey, string transitionId, string commentAdf)
        {
            var sb = new StringBuilder(128);
            sb.Append("{\"transition\":{\"id\":\"").Append(JiraIssueDraft.JsonEscape(transitionId)).Append("\"}");
            if (!string.IsNullOrWhiteSpace(commentAdf))
                sb.Append(",\"update\":{\"comment\":[{\"add\":{\"body\":").Append(commentAdf).Append("}}]}");
            sb.Append('}');

            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbPOST,
                $"/rest/api/3/issue/{UnityWebRequest.EscapeURL(issueKey)}/transitions",
                sb.ToString());

            return response.Success ? null : BuildIssueError(response);
        }

        /// <summary>Adds a comment (ADF body). Returns null on success or a friendly error.</summary>
        public async Task<string> AddCommentAsync(string issueKey, string commentAdf)
        {
            string body = "{\"body\":" + commentAdf + "}";
            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbPOST,
                $"/rest/api/3/issue/{UnityWebRequest.EscapeURL(issueKey)}/comment",
                body);

            return response.Success ? null : BuildIssueError(response);
        }

        /// <summary>Searches users by display name / email (for @mentions).</summary>
        public async Task<List<JiraUser>> SearchUsersAsync(string query)
        {
            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbGET,
                $"/rest/api/3/user/search?query={UnityWebRequest.EscapeURL(query ?? string.Empty)}&maxResults=20",
                null);

            if (!response.Success || string.IsNullOrWhiteSpace(response.Body))
                return new List<JiraUser>();

            try
            {
                var wrapped = JsonUtility.FromJson<JiraUserList>("{\"items\":" + response.Body + "}");
                return ToList(wrapped?.items);
            }
            catch
            {
                return new List<JiraUser>();
            }
        }

        // --- HTTP core ------------------------------------------------------

        private sealed class JiraResponse
        {
            public bool Success;
            public long StatusCode;
            public string Body;
            public string Error;
        }

        private Task<JiraResponse> SendAsync(string method, string relativePath, string jsonBody)
        {
            string url = _baseUrl + relativePath;

            UnityWebRequest request;
            if (method == UnityWebRequest.kHttpVerbPOST || method == UnityWebRequest.kHttpVerbPUT)
            {
                request = new UnityWebRequest(url, method)
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody ?? string.Empty)),
                    downloadHandler = new DownloadHandlerBuffer()
                };
                request.SetRequestHeader("Content-Type", "application/json");
            }
            else
            {
                request = UnityWebRequest.Get(url);
            }

            request.timeout = 30;
            return AwaitRequest(request);
        }

        private Task<JiraResponse> AwaitRequest(UnityWebRequest request)
        {
            request.SetRequestHeader("Authorization", _authProvider.BuildAuthorizationHeader());
            request.SetRequestHeader("Accept", "application/json");

            var completion = new TaskCompletionSource<JiraResponse>();
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();

            void PollRequest()
            {
                if (!operation.isDone)
                    return;

                EditorApplication.update -= PollRequest;

                try
                {
                    bool success = request.result == UnityWebRequest.Result.Success;
                    completion.TrySetResult(new JiraResponse
                    {
                        Success = success,
                        StatusCode = request.responseCode,
                        Body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty,
                        Error = success ? null : BuildFriendlyError(request)
                    });
                }
                finally
                {
                    request.Dispose();
                }
            }

            EditorApplication.update += PollRequest;
            return completion.Task;
        }

        private static void ThrowIfFailed(JiraResponse response, string fallbackMessage)
        {
            if (!response.Success)
                throw new Exception(string.IsNullOrEmpty(response.Error) ? fallbackMessage : response.Error);
        }

        private static List<T> ToList<T>(T[] values)
        {
            return values != null ? new List<T>(values) : new List<T>();
        }

        private static string BuildIssueError(JiraResponse response)
        {
            string parsed = ExtractJiraErrors(response.Body);
            if (!string.IsNullOrEmpty(parsed))
                return parsed;

            return string.IsNullOrEmpty(response.Error)
                ? $"Falha HTTP {response.StatusCode}."
                : response.Error;
        }

        private static string ExtractJsonPrimitive(
            string json,
            string fieldId)
        {
            if (string.IsNullOrWhiteSpace(json) ||
                string.IsNullOrWhiteSpace(fieldId))
            {
                return string.Empty;
            }

            string token = "\"" + fieldId + "\"";
            int fieldIndex = json.IndexOf(token, StringComparison.Ordinal);
            if (fieldIndex < 0)
                return string.Empty;

            int colon = json.IndexOf(':', fieldIndex + token.Length);
            if (colon < 0)
                return string.Empty;

            int start = colon + 1;
            while (start < json.Length && char.IsWhiteSpace(json[start]))
                start++;

            if (start >= json.Length ||
                json.IndexOf("null", start, StringComparison.Ordinal) == start)
            {
                return string.Empty;
            }

            int end = start;
            while (end < json.Length &&
                   json[end] != ',' &&
                   json[end] != '}' &&
                   !char.IsWhiteSpace(json[end]))
            {
                end++;
            }

            return json.Substring(start, end - start).Trim('"');
        }

        private static string ExtractJsonFieldIdentifier(
            string json,
            string fieldId)
        {
            if (string.IsNullOrWhiteSpace(json) ||
                string.IsNullOrWhiteSpace(fieldId))
            {
                return string.Empty;
            }

            string token = "\"" + fieldId + "\"";
            int fieldIndex = json.IndexOf(token, StringComparison.Ordinal);
            if (fieldIndex < 0)
                return string.Empty;

            int colon = json.IndexOf(':', fieldIndex + token.Length);
            if (colon < 0)
                return string.Empty;

            int start = colon + 1;
            while (start < json.Length && char.IsWhiteSpace(json[start]))
                start++;

            if (start >= json.Length || json[start] != '{')
                return ExtractJsonPrimitive(json, fieldId);

            int objectEnd = json.IndexOf('}', start + 1);
            if (objectEnd < 0)
                return string.Empty;

            string objectJson =
                json.Substring(start, objectEnd - start + 1);
            return ExtractJsonPrimitive(objectJson, "id");
        }

        private static List<JiraFieldMeta> ParseFieldMap(string json)
        {
            var fields = new List<JiraFieldMeta>();
            if (string.IsNullOrWhiteSpace(json))
                return fields;

            int fieldsToken = json.IndexOf(
                "\"fields\"",
                StringComparison.Ordinal);
            if (fieldsToken < 0)
                return fields;

            int objectStart = json.IndexOf('{', fieldsToken + 8);
            int objectEnd = FindMatchingJsonDelimiter(
                json,
                objectStart,
                '{',
                '}');
            if (objectStart < 0 || objectEnd < 0)
                return fields;

            int cursor = objectStart + 1;
            while (cursor < objectEnd)
            {
                SkipJsonWhitespaceAndCommas(json, ref cursor, objectEnd);
                if (cursor >= objectEnd || json[cursor] != '"')
                    break;

                int keyStart = ++cursor;
                while (cursor < objectEnd)
                {
                    if (json[cursor] == '\\')
                    {
                        cursor += 2;
                        continue;
                    }
                    if (json[cursor] == '"')
                        break;
                    cursor++;
                }
                if (cursor >= objectEnd)
                    break;

                string fieldId = json.Substring(
                    keyStart,
                    cursor - keyStart);
                cursor++;
                SkipJsonWhitespace(json, ref cursor, objectEnd);
                if (cursor >= objectEnd || json[cursor] != ':')
                    break;

                cursor++;
                SkipJsonWhitespace(json, ref cursor, objectEnd);
                if (cursor >= objectEnd || json[cursor] != '{')
                {
                    SkipJsonValue(json, ref cursor, objectEnd);
                    continue;
                }

                int fieldEnd = FindMatchingJsonDelimiter(
                    json,
                    cursor,
                    '{',
                    '}');
                if (fieldEnd < 0 || fieldEnd > objectEnd)
                    break;

                string fieldObject = json.Substring(
                    cursor,
                    fieldEnd - cursor + 1);
                string fieldContent = fieldObject.Length > 2
                    ? fieldObject.Substring(1, fieldObject.Length - 2).Trim()
                    : string.Empty;
                string enriched =
                    "{\"fieldId\":\"" +
                    JiraIssueDraft.JsonEscape(fieldId) +
                    "\"" +
                    (fieldContent.Length > 0
                        ? "," + fieldContent
                        : string.Empty) +
                    "}";

                try
                {
                    JiraFieldMeta field =
                        JsonUtility.FromJson<JiraFieldMeta>(enriched);
                    if (field != null)
                        fields.Add(field);
                }
                catch
                {
                    // Ignore an unsupported field and keep the remaining ones.
                }

                cursor = fieldEnd + 1;
            }

            return fields;
        }

        private static int FindMatchingJsonDelimiter(
            string json,
            int start,
            char opening,
            char closing)
        {
            if (string.IsNullOrEmpty(json) ||
                start < 0 ||
                start >= json.Length ||
                json[start] != opening)
            {
                return -1;
            }

            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = start; i < json.Length; i++)
            {
                char character = json[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (character == '\\')
                    {
                        escaped = true;
                    }
                    else if (character == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (character == '"')
                {
                    inString = true;
                }
                else if (character == opening)
                {
                    depth++;
                }
                else if (character == closing && --depth == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void SkipJsonWhitespace(
            string json,
            ref int cursor,
            int end)
        {
            while (cursor < end && char.IsWhiteSpace(json[cursor]))
                cursor++;
        }

        private static void SkipJsonWhitespaceAndCommas(
            string json,
            ref int cursor,
            int end)
        {
            while (cursor < end &&
                   (char.IsWhiteSpace(json[cursor]) ||
                    json[cursor] == ','))
            {
                cursor++;
            }
        }

        private static List<JiraUser> ParseUserArray(
            JiraResponse response)
        {
            if (response == null ||
                !response.Success ||
                string.IsNullOrWhiteSpace(response.Body))
            {
                return new List<JiraUser>();
            }

            try
            {
                var wrapped = JsonUtility.FromJson<JiraUserList>(
                    "{\"items\":" + response.Body + "}");
                return ToList(wrapped?.items);
            }
            catch
            {
                return new List<JiraUser>();
            }
        }

        private static void SkipJsonValue(
            string json,
            ref int cursor,
            int end)
        {
            bool inString = false;
            bool escaped = false;
            while (cursor < end)
            {
                char character = json[cursor];
                if (inString)
                {
                    if (escaped)
                        escaped = false;
                    else if (character == '\\')
                        escaped = true;
                    else if (character == '"')
                        inString = false;
                }
                else if (character == '"')
                {
                    inString = true;
                }
                else if (character == ',')
                {
                    return;
                }

                cursor++;
            }
        }

        private static string ExtractJiraErrors(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return null;

            var messages = new List<string>();

            try
            {
                var payload = JsonUtility.FromJson<JiraErrorPayload>(body);
                if (payload?.errorMessages != null)
                    messages.AddRange(payload.errorMessages);
            }
            catch
            {
                // Ignore: fall back to the field-level extraction below.
            }

            int errorsIndex = body.IndexOf("\"errors\":{", StringComparison.Ordinal);
            if (errorsIndex >= 0)
            {
                int open = body.IndexOf('{', errorsIndex);
                int close = open >= 0 ? body.IndexOf('}', open) : -1;
                if (open >= 0 && close > open + 1)
                {
                    string inner = body.Substring(open + 1, close - open - 1).Trim();
                    if (inner.Length > 0)
                        messages.Add(inner.Replace("\"", string.Empty));
                }
            }

            return messages.Count > 0 ? string.Join("\n", messages) : null;
        }

        private static string BuildFriendlyError(UnityWebRequest request)
        {
            switch (request.responseCode)
            {
                case 0:
                    return "Não foi possível alcançar o Jira. Verifique a URL, a internet, VPN ou proxy da empresa.";
                case 400:
                    return "O Jira rejeitou a solicitação. Confira os dados informados.";
                case 401:
                    return "E-mail ou API Token inválido, expirado ou bloqueado pela organização.";
                case 403:
                    return "A conta foi autenticada, mas não possui permissão para acessar este recurso.";
                case 404:
                    return "O endpoint não foi encontrado. Confirme se a URL pertence ao Jira Cloud.";
                case 429:
                    return "O Jira limitou temporariamente as requisições. Tente novamente mais tarde.";
                default:
                    string serverMessage = request.downloadHandler != null
                        ? request.downloadHandler.text
                        : request.error;
                    return $"Falha HTTP {request.responseCode}: {serverMessage}";
            }
        }

        private static string NormalizeBaseUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A URL do Jira é obrigatória.", nameof(value));

            string normalized = value.Trim().TrimEnd('/');
            if (!Uri.TryCreate(normalized, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                throw new ArgumentException("Informe uma URL válida, como https://empresa.atlassian.net.", nameof(value));
            }

            return normalized;
        }

        private static string EscapeJqlString(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }
    }
}
