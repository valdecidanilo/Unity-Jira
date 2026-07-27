using System;
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

        public Task<JiraConnectionResult> TestConnectionAsync()
        {
            return SendGetAsync<JiraUser>("/rest/api/3/myself");
        }

        private Task<JiraConnectionResult> SendGetAsync<T>(string relativePath) where T : class
        {
            var completion = new TaskCompletionSource<JiraConnectionResult>();
            var request = UnityWebRequest.Get(_baseUrl + relativePath);
            request.timeout = 20;
            request.SetRequestHeader("Authorization", _authProvider.BuildAuthorizationHeader());
            request.SetRequestHeader("Accept", "application/json");

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();

            void PollRequest()
            {
                if (!operation.isDone)
                    return;

                EditorApplication.update -= PollRequest;

                try
                {
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        T payload = JsonUtility.FromJson<T>(request.downloadHandler.text);
                        completion.TrySetResult(JiraConnectionResult.Ok(payload as JiraUser));
                        return;
                    }

                    completion.TrySetResult(JiraConnectionResult.Fail(
                        request.responseCode,
                        BuildFriendlyError(request)));
                }
                catch (Exception exception)
                {
                    completion.TrySetResult(JiraConnectionResult.Fail(
                        request.responseCode,
                        $"Não foi possível processar a resposta do Jira: {exception.Message}"));
                }
                finally
                {
                    request.Dispose();
                }
            }

            EditorApplication.update += PollRequest;
            return completion.Task;
        }

        private static string BuildFriendlyError(UnityWebRequest request)
        {
            switch (request.responseCode)
            {
                case 0:
                    return "Não foi possível alcançar o Jira. Verifique a URL, a internet, VPN ou proxy da empresa.";
                case 400:
                    return "O Jira rejeitou a solicitação. Confira a URL informada.";
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
