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
    /// Client for the OpenAI Chat Completions API (https://api.openai.com/v1/chat/completions).
    /// Each user supplies their own API key.
    /// </summary>
    internal sealed class OpenAiClient : IAiIssueClient
    {
        private const string Endpoint = "https://api.openai.com/v1/chat/completions";

        private readonly string _apiToken;
        private readonly string _model;

        public OpenAiClient(string apiToken, string model)
        {
            _apiToken = apiToken?.Trim() ?? string.Empty;
            _model = string.IsNullOrWhiteSpace(model) ? "gpt-4o" : model.Trim();
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
            sb.Append("\"max_completion_tokens\":2048,");
            sb.Append("\"response_format\":{\"type\":\"json_object\"},");
            sb.Append("\"messages\":[");
            sb.Append("{\"role\":\"system\",\"content\":\"").Append(JiraIssueDraft.JsonEscape(systemText)).Append("\"},");
            sb.Append("{\"role\":\"user\",\"content\":\"").Append(JiraIssueDraft.JsonEscape(userContent)).Append("\"}");
            sb.Append("]}");
            return sb.ToString();
        }

        private static string ExtractText(string responseText)
        {
            var response = JsonUtility.FromJson<OpenAiChatResponse>(responseText);

            if (response == null || response.choices == null || response.choices.Length == 0)
                throw new Exception("A IA não retornou conteúdo.");

            string content = response.choices[0].message?.content;
            if (string.IsNullOrWhiteSpace(content))
                throw new Exception("A IA não retornou texto.");

            return content;
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
            request.SetRequestHeader("Authorization", "Bearer " + _apiToken);

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
                    return "API Key da OpenAI inválida.";
                case 400:
                    return message ?? "Requisição inválida para a IA.";
                case 404:
                    return message ?? "Modelo da OpenAI não encontrado ou sem acesso.";
                case 429:
                    return "Limite/cota da OpenAI atingido. Verifique seu plano ou tente mais tarde.";
                default:
                    if (!string.IsNullOrWhiteSpace(message))
                        return message;
                    return statusCode > 0
                        ? $"Falha HTTP {statusCode} ao contatar a OpenAI."
                        : $"Não foi possível contatar a OpenAI: {fallback}";
            }
        }
    }
}
