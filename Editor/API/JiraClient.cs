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
                List<JiraIssueType> types = ToList(page?.values);
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
            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbGET,
                $"/rest/api/3/issue/createmeta/{UnityWebRequest.EscapeURL(projectKey)}/issuetypes/{UnityWebRequest.EscapeURL(issueTypeId)}?maxResults=200",
                null);

            if (!response.Success)
                return new List<JiraFieldMeta>();

            var page = JsonUtility.FromJson<JiraFieldMetaPage>(response.Body);
            return ToList(page?.values);
        }

        public async Task<List<JiraUser>> GetAssignableUsersAsync(string projectKey)
        {
            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbGET,
                $"/rest/api/3/user/assignable/search?project={UnityWebRequest.EscapeURL(projectKey)}&maxResults=50",
                null);

            if (!response.Success || string.IsNullOrWhiteSpace(response.Body))
                return new List<JiraUser>();

            try
            {
                // Body is a top-level JSON array; wrap it so JsonUtility can read it.
                var wrapped = JsonUtility.FromJson<JiraUserList>("{\"items\":" + response.Body + "}");
                return ToList(wrapped?.items);
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

        public async Task<List<JiraSprint>> GetActiveSprintsAsync(int boardId)
        {
            JiraResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbGET,
                $"/rest/agile/1.0/board/{boardId}/sprint?state=active&maxResults=50",
                null);

            if (!response.Success)
                return new List<JiraSprint>();

            var page = JsonUtility.FromJson<JiraSprintPage>(response.Body);
            return ToList(page?.values);
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
                string jql = UnityWebRequest.EscapeURL($"parent = \"{epicKey}\"");
                JiraResponse search = await SendAsync(
                    UnityWebRequest.kHttpVerbGET,
                    $"/rest/api/3/search?jql={jql}&fields=status&maxResults=200",
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
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return "Arquivo de anexo não encontrado.";

            byte[] bytes;
            try { bytes = File.ReadAllBytes(filePath); }
            catch (Exception exception) { return $"Não foi possível ler o anexo: {exception.Message}"; }

            string fileName = Path.GetFileName(filePath);
            var form = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", bytes, fileName, "application/octet-stream")
            };

            UnityWebRequest request = UnityWebRequest.Post(_baseUrl + $"/rest/api/3/issue/{issueKey}/attachments", form);
            request.timeout = 60;
            request.SetRequestHeader("X-Atlassian-Token", "no-check");

            JiraResponse response = await AwaitRequest(request);
            return response.Success ? null : BuildIssueError(response);
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
    }
}
