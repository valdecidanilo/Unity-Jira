using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using OxenteGames.JiraCommunication.Models;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace OxenteGames.JiraCommunication.AI
{
    /// <summary>
    /// Client for the Anthropic Messages API (https://api.anthropic.com/v1/messages).
    /// Each user supplies their own API token.
    /// </summary>
    internal sealed class ClaudeClient : IAiIssueClient
    {
        private const string Endpoint = "https://api.anthropic.com/v1/messages";
        private const string ApiVersion = "2023-06-01";

        private readonly string _apiToken;
        private readonly string _model;

        public ClaudeClient(string apiToken, string model)
        {
            _apiToken = apiToken?.Trim() ?? string.Empty;
            _model = string.IsNullOrWhiteSpace(model) ? "claude-sonnet-5" : model.Trim();
        }

        public async Task<AiSuggestion> SuggestIssueAsync(
            string userInput,
            string projectName,
            string issueTypeName,
            IReadOnlyList<string> priorityNames,
            bool portuguese)
        {
            if (string.IsNullOrWhiteSpace(_apiToken))
                throw new Exception("Token de IA não configurado.");

            string body = BuildRequestBody(
                AiPrompt.BuildSystem(priorityNames, portuguese),
                AiPrompt.BuildContext(userInput, projectName, issueTypeName));

            string responseText = await SendAsync(body);
            return AiPrompt.Parse(ExtractText(responseText));
        }

        private string BuildRequestBody(string systemText, string userContent)
        {
            var sb = new StringBuilder(512);
            sb.Append('{');
            sb.Append("\"model\":\"").Append(JiraIssueDraft.JsonEscape(_model)).Append("\",");
            sb.Append("\"max_tokens\":2048,");
            sb.Append("\"system\":\"").Append(JiraIssueDraft.JsonEscape(systemText)).Append("\",");
            sb.Append("\"messages\":[{\"role\":\"user\",\"content\":\"")
              .Append(JiraIssueDraft.JsonEscape(userContent))
              .Append("\"}]");
            sb.Append('}');
            return sb.ToString();
        }

        private static string ExtractText(string responseText)
        {
            var message = JsonUtility.FromJson<ClaudeMessageResponse>(responseText);

            if (message == null || message.content == null || message.content.Length == 0)
                throw new Exception("A IA não retornou conteúdo.");

            if (message.stop_reason == "refusal")
                throw new Exception("A IA recusou a solicitação. Reformule a descrição.");

            foreach (ClaudeContentBlock block in message.content)
            {
                if (block != null && block.type == "text" && !string.IsNullOrWhiteSpace(block.text))
                    return block.text;
            }

            throw new Exception("A IA não retornou texto.");
        }

        private Task<string> SendAsync(string jsonBody)
        {
            var completion = new TaskCompletionSource<string>();

            var request = new UnityWebRequest(Endpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 60
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("x-api-key", _apiToken);
            request.SetRequestHeader("anthropic-version", ApiVersion);

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();

            void Poll()
            {
                if (!operation.isDone)
                    return;

                EditorApplication.update -= Poll;

                try
                {
                    string bodyText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        completion.TrySetResult(bodyText);
                        return;
                    }

                    completion.TrySetException(new Exception(BuildFriendlyError(request.responseCode, bodyText, request.error)));
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
                finally
                {
                    request.Dispose();
                }
            }

            EditorApplication.update += Poll;
            return completion.Task;
        }

        private static string BuildFriendlyError(long statusCode, string body, string fallback)
        {
            string message = AiPrompt.ExtractErrorMessage(body);

            switch (statusCode)
            {
                case 401:
                    return "API Key da Anthropic inválida.";
                case 400:
                    return message ?? "Requisição inválida para a IA.";
                case 429:
                    return "Limite de uso da Anthropic atingido. Tente novamente mais tarde.";
                case 529:
                    return "O serviço da Anthropic está sobrecarregado. Tente novamente em instantes.";
                default:
                    if (!string.IsNullOrWhiteSpace(message))
                        return message;
                    return statusCode > 0
                        ? $"Falha HTTP {statusCode} ao contatar a Anthropic."
                        : $"Não foi possível contatar a Anthropic: {fallback}";
            }
        }
    }
}
