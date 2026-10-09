using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Telemedicina
{
    public sealed class ApiResult<T>
    {
        public bool Ok; public long Code; public T Data; public string Message;
    }

    public sealed class ApiClient : MonoBehaviour
    {
        public string BaseUrl { get; private set; } = "http://localhost:8000";
        public Usuario User { get; private set; }
        private string token;
        private DateTimeOffset expiresAt;
        private int sessionGeneration;
        private readonly HashSet<UnityWebRequest> requests = new HashSet<UnityWebRequest>();
        public event Action SessionExpired;
        public bool HasToken => !string.IsNullOrEmpty(token) && DateTimeOffset.UtcNow < expiresAt;

        internal string SessionToken => HasToken ? token : null;
        public void Configure(string url) { BaseUrl = url; }
        public void Authenticate(TokenData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.access_token) || data.expires_in <= 0 ||
                !string.Equals(data.token_type, "bearer", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Respuesta de autenticación incompatible.");
            token = data.access_token; expiresAt = DateTimeOffset.UtcNow.AddSeconds(data.expires_in);
        }
        public void SetUser(Usuario user) { User = user; }
        public void ClearSession()
        {
            token = null; User = null; expiresAt = default; sessionGeneration++;
            foreach (var request in requests) request.Abort();
        }
        private void Update()
        {
            if (!string.IsNullOrEmpty(token) && DateTimeOffset.UtcNow >= expiresAt)
            { ClearSession(); SessionExpired?.Invoke(); }
        }
        public IEnumerator Send<T>(string method, string path, object body, Action<ApiResult<T>> completed, bool authenticated = true)
        {
            if (authenticated && !HasToken)
            {
                completed(new ApiResult<T> { Code = 401, Message = ApiProtocol.ErrorMessage(401) });
                yield break;
            }
            var generation = sessionGeneration;
            using (var request = new UnityWebRequest(BaseUrl + "/api" + path, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 15;
                request.redirectLimit = 0; // Nunca reenviar un token a otro origen por una redirección.
                request.SetRequestHeader("Accept", "application/json");
                if (authenticated) request.SetRequestHeader("Authorization", "Bearer " + token);
                if (body != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(ApiProtocol.Encode(body)));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                requests.Add(request);
                yield return request.SendWebRequest();
                requests.Remove(request);
                if (generation != sessionGeneration)
                { completed(new ApiResult<T> { Code = 401, Message = ApiProtocol.ErrorMessage(401) }); yield break; }
                var result = new ApiResult<T> { Code = request.responseCode };
                if (request.result == UnityWebRequest.Result.Success && result.Code >= 200 && result.Code < 300)
                {
                    try
                    {
                        result.Data = string.IsNullOrWhiteSpace(request.downloadHandler.text)
                            ? default : ApiProtocol.Decode<T>(request.downloadHandler.text);
                        result.Ok = true;
                    }
                    catch (Exception)
                    { result.Message = "Respuesta del servidor no compatible."; }
                }
                else result.Message = ApiProtocol.ErrorMessage(result.Code);
                if (authenticated && result.Code == 401)
                { ClearSession(); SessionExpired?.Invoke(); }
                completed(result);
            }
        }
        private void OnDestroy() { ClearSession(); }
    }
}
