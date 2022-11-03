using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.Wrap;

namespace Pidan.Resiliense.Http
{
    /// <summary>
    /// HttpClient wrapper that integrates Retry and Circuit
    /// breaker policies when invoking HTTP services. 
    /// Based on Polly library: https://github.com/App-vNext/Polly
    /// </summary>
    public class ResilientHttpClient : IHttpClient
    {
        private readonly HttpClient _client;
        private readonly ILogger<ResilientHttpClient> _logger;
        private readonly Func<string, IEnumerable<IAsyncPolicy>> _policyCreator;
        private readonly ConcurrentDictionary<string, PolicyWrap> _policyWrappers;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ResilientHttpClient(Func<string, IEnumerable<IAsyncPolicy>> policyCreator,
            ILogger<ResilientHttpClient> logger,
            IHttpContextAccessor httpContextAccessor,
            HttpMessageHandler httpMessageHandler = null)
        {
            _client = httpMessageHandler == null
                ? new HttpClient()
                : new HttpClient(httpMessageHandler);
            _logger = logger;
            _policyCreator = policyCreator;
            _policyWrappers = new ConcurrentDictionary<string, PolicyWrap>();
            _httpContextAccessor = httpContextAccessor;
        }


        public Task<HttpResponseMessage> PostAsync<T>(string uri, T item, string authorizationToken = null,
            string requestId = null, string authorizationMethod = "Bearer")
        {
            HttpRequestMessage Func() => GetHttpRequestMessage(uri, HttpMethod.Post, item);
            return DoPostPutAsync(HttpMethod.Post, uri, Func, authorizationToken, requestId, authorizationMethod);
        }

        public Task<HttpResponseMessage> PostAsync(string uri, Dictionary<string, string> dictionary,
            string authorizationToken = null, string requestId = null,
            string authorizationMethod = "Bearer")
        {
            HttpRequestMessage Func() => GetHttpRequestMessage(uri, HttpMethod.Post, dictionary);
            return DoPostPutAsync(HttpMethod.Post, uri, Func, authorizationToken, requestId, authorizationMethod);
        }

        public Task<HttpResponseMessage> PutAsync<T>(string uri, T item, string authorizationToken = null,
            string requestId = null, string authorizationMethod = "Bearer")
        {
            HttpRequestMessage Func() => GetHttpRequestMessage(uri, HttpMethod.Put, item);
            return DoPostPutAsync(HttpMethod.Put, uri, Func, authorizationToken, requestId, authorizationMethod);
        }

        public Task<HttpResponseMessage> PutAsync(string uri, Dictionary<string, string> dictionary,
            string authorizationToken = null, string requestId = null,
            string authorizationMethod = "Bearer")
        {
            HttpRequestMessage Func() => GetHttpRequestMessage(uri, HttpMethod.Put, dictionary);
            return DoPostPutAsync(HttpMethod.Put, uri, Func, authorizationToken, requestId, authorizationMethod);
        }

        public Task<HttpResponseMessage> DeleteAsync(string uri, string authorizationToken = null,
            string requestId = null, string authorizationMethod = "Bearer")
        {
            var origin = GetOriginFromUri(uri);

            return HttpInvoker(origin, async ctx =>
            {
                var requestMessage = new HttpRequestMessage(HttpMethod.Delete, uri);

                SetAuthorizationHeader(requestMessage);

                if (authorizationToken != null)
                {
                    requestMessage.Headers.Authorization =
                        new AuthenticationHeaderValue(authorizationMethod, authorizationToken);
                }

                if (requestId != null)
                {
                    requestMessage.Headers.Add("x-requestid", requestId);
                }

                return await _client.SendAsync(requestMessage);
            });
        }


        public Task<string> GetStringAsync(string uri, string authorizationToken = null,
            string authorizationMethod = "Bearer")
        {
            var origin = GetOriginFromUri(uri);

            return HttpInvoker(origin, async ctx =>
            {
                var requestMessage = new HttpRequestMessage(HttpMethod.Get, uri);

                SetAuthorizationHeader(requestMessage);

                if (authorizationToken != null)
                {
                    requestMessage.Headers.Authorization =
                        new AuthenticationHeaderValue(authorizationMethod, authorizationToken);
                }

                var response = await _client.SendAsync(requestMessage);

                // raise exception if HttpResponseCode 500 
                // needed for circuit breaker to track fails

                if (response.StatusCode == HttpStatusCode.InternalServerError)
                {
                    throw new HttpRequestException();
                }

                return await response.Content.ReadAsStringAsync();
            });
        }

        private Task<HttpResponseMessage> DoPostPutAsync(HttpMethod method, string uri,
            Func<HttpRequestMessage> createRequestMessage,
            string authorizationToken = null, string requestId = null, string authorizationMethod = "Bearer")
        {
            if (method != HttpMethod.Post && method != HttpMethod.Put)
            {
                throw new ArgumentException("Value must be either post or put.", nameof(method));
            }

            // a new StringContent must be created for each retry 
            // as it is disposed after each call
            var origin = GetOriginFromUri(uri);

            return HttpInvoker(origin, async ctx =>
            {
                var requestMessage = createRequestMessage();

                SetAuthorizationHeader(requestMessage);

                if (authorizationToken != null)
                {
                    requestMessage.Headers.Authorization =
                        new AuthenticationHeaderValue(authorizationMethod, authorizationToken);
                }

                if (requestId != null)
                {
                    requestMessage.Headers.Add("x-requestid", requestId);
                }

                var response = await _client.SendAsync(requestMessage);

                // raise exception if HttpResponseCode 500 
                // needed for circuit breaker to track fails

                if (response.StatusCode == HttpStatusCode.InternalServerError)
                {
                    throw new HttpRequestException();
                }

                return response;
            });
        }

        private async Task<T> HttpInvoker<T>(string origin, Func<Context, Task<T>> action)
        {
            var normalizedOrigin = NormalizeOrigin(origin);

            if (!_policyWrappers.TryGetValue(normalizedOrigin, out var policyWrap))
            {
                var asyncPolicies = _policyCreator(normalizedOrigin).ToArray();
                policyWrap = Policy.WrapAsync(asyncPolicies);
                _policyWrappers.TryAdd(normalizedOrigin, policyWrap);
            }

            // Executes the action applying all 
            // the policies defined in the wrapper
            return await policyWrap.Execute(action, context: new Context(normalizedOrigin));
        }

        private HttpRequestMessage GetHttpRequestMessage(string url, HttpMethod method, Func<HttpContent> func)
        {
            var requestMessage = new HttpRequestMessage(method, url)
            {
                Content = func()
            };

            return requestMessage;
        }

        private HttpRequestMessage GetHttpRequestMessage<T>(string url, HttpMethod method, T item)
        {
            return GetHttpRequestMessage(url, method, () => new StringContent(JsonConvert.SerializeObject(item),
                System.Text.Encoding.UTF8,
                "application/json"));
        }

        private HttpRequestMessage GetHttpRequestMessage(string url, HttpMethod method,
            Dictionary<string, string> dictionary)
        {
            return GetHttpRequestMessage(url, method, () => new FormUrlEncodedContent(dictionary));
        }


        private static string NormalizeOrigin(string origin)
        {
            return origin?.Trim().ToLower();
        }

        private static string GetOriginFromUri(string uri)
        {
            var url = new Uri(uri);

            var origin = $"{url.Scheme}://{url.DnsSafeHost}:{url.Port}";

            return origin;
        }

        private void SetAuthorizationHeader(HttpRequestMessage requestMessage)
        {
            var authorizationHeader = _httpContextAccessor?.HttpContext?.Request?.Headers["Authorization"];
            if (!string.IsNullOrEmpty(authorizationHeader))
            {
                requestMessage.Headers.Add("Authorization", new List<string> { authorizationHeader });
            }
        }
    }
}