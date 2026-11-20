#if MU3LIBRARY_UNITASK_SUPPORT
using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Mu3Library.WebRequest;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mu3Library.Tests
{
    /// <summary>
    /// Requests against a server on the loopback address, so HTTP error responses and a refused
    /// connection can be observed without a network.
    /// </summary>
    public class WebRequestManagerTests
    {
        private const double MaxSeconds = 10.0;
        private const string HeaderName = "Retry-After";
        private const string HeaderValue = "7";

        private WebRequestManager _manager;
        private TestHttpServer _server;

        /// <summary>
        /// Answers every request with one fixed status, or holds the connection open without an answer.
        /// </summary>
        private sealed class TestHttpServer : IDisposable
        {
            private readonly TcpListener _listener;
            private readonly int _statusCode;
            private readonly bool _holdResponse;
            private int _requestCount;

            public TestHttpServer(int statusCode, bool holdResponse = false)
            {
                _statusCode = statusCode;
                _holdResponse = holdResponse;
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                _ = AcceptLoopAsync();
            }

            public string Url => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/resource";
            public int RequestCount => Volatile.Read(ref _requestCount);

            public void Dispose()
            {
                _listener.Stop();
            }

            private async Task AcceptLoopAsync()
            {
                while (true)
                {
                    TcpClient client;
                    try
                    {
                        client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        return;
                    }

                    _ = Task.Run(() => Respond(client));
                }
            }

            private void Respond(TcpClient client)
            {
                using (client)
                {
                    try
                    {
                        NetworkStream stream = client.GetStream();
                        stream.ReadTimeout = 5000;
                        ReadRequestHead(stream);
                        Interlocked.Increment(ref _requestCount);

                        if (_holdResponse)
                        {
                            // Keeps the connection open until the client gives up on it.
                            stream.ReadTimeout = Timeout.Infinite;
                            while (stream.ReadByte() >= 0)
                            {
                            }

                            return;
                        }

                        byte[] body = Encoding.UTF8.GetBytes($"status {_statusCode}");
                        string head =
                            $"HTTP/1.1 {_statusCode} Test\r\n" +
                            "Content-Type: text/plain\r\n" +
                            $"Content-Length: {body.Length}\r\n" +
                            $"{HeaderName}: {HeaderValue}\r\n" +
                            "Connection: close\r\n\r\n";
                        byte[] headBytes = Encoding.ASCII.GetBytes(head);
                        stream.Write(headBytes, 0, headBytes.Length);
                        stream.Write(body, 0, body.Length);
                        stream.Flush();
                    }
                    catch (Exception)
                    {
                        // The client went away; there is nobody left to answer.
                    }
                }
            }

            private static void ReadRequestHead(NetworkStream stream)
            {
                int matched = 0;
                byte[] terminator = { (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' };
                while (matched < terminator.Length)
                {
                    int value = stream.ReadByte();
                    if (value < 0)
                    {
                        return;
                    }

                    matched = value == terminator[matched] ? matched + 1 : (value == terminator[0] ? 1 : 0);
                }
            }
        }

        [SetUp]
        public void SetUp()
        {
            _manager = new WebRequestManager();
        }

        [TearDown]
        public void TearDown()
        {
            _server?.Dispose();
            _server = null;
        }

        private static IEnumerator RunUntil(Func<bool> condition, string description)
        {
            double start = EditorApplication.timeSinceStartup;
            while (!condition() && EditorApplication.timeSinceStartup - start < MaxSeconds)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
            }

            Assert.IsTrue(condition(), $"Waited {MaxSeconds} seconds until {description}.");
        }

        /// <summary>
        /// A port nothing listens on: bound once to get a free one, then released again.
        /// </summary>
        private static string CreateRefusedUrl()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            return $"http://127.0.0.1:{port}/resource";
        }

        [UnityTest]
        public IEnumerator GetResultAsync_HttpErrorResponse_KeepsStatusCodeAndHeadersLikeTheCallbackApi([Values(401, 404, 500)] int statusCode)
        {
            _server = new TestHttpServer(statusCode);
            LogAssert.Expect(LogType.Error, new Regex("WebRequest GET failed"));
            LogAssert.Expect(LogType.Error, new Regex("WebRequest GET failed"));

            UniTask<WebRequestResult<string>> request = _manager.GetResultAsync<string>(_server.Url);
            yield return RunUntil(() => request.Status != UniTaskStatus.Pending, "GetResultAsync completed");
            WebRequestResult<string> asyncResult = request.GetAwaiter().GetResult();

            bool hasCallbackResult = false;
            WebRequestResult<string> callbackResult = default;
            _manager.GetWithResult<string>(_server.Url, result =>
            {
                callbackResult = result;
                hasCallbackResult = true;
            });
            yield return RunUntil(() => hasCallbackResult, "GetWithResult answered");

            Assert.IsFalse(asyncResult.IsSuccess);
            Assert.AreEqual(statusCode, asyncResult.StatusCode);
            Assert.IsNotNull(asyncResult.ResponseHeaders, "An HTTP error response carries its headers.");
            Assert.IsTrue(asyncResult.ResponseHeaders.TryGetValue(HeaderName, out string headerValue));
            Assert.AreEqual(HeaderValue, headerValue);

            Assert.AreEqual(callbackResult.IsSuccess, asyncResult.IsSuccess);
            Assert.AreEqual(callbackResult.StatusCode, asyncResult.StatusCode);
            Assert.AreEqual(callbackResult.ResponseHeaders[HeaderName], headerValue);
        }

        [UnityTest]
        public IEnumerator GetResultAsync_ConnectionRefused_AnswersWithoutAResponseLikeTheCallbackApi()
        {
            string url = CreateRefusedUrl();
            LogAssert.Expect(LogType.Error, new Regex("WebRequest GET failed"));
            LogAssert.Expect(LogType.Error, new Regex("WebRequest GET failed"));

            UniTask<WebRequestResult<string>> request = _manager.GetResultAsync<string>(url);
            yield return RunUntil(() => request.Status != UniTaskStatus.Pending, "GetResultAsync completed");
            WebRequestResult<string> asyncResult = request.GetAwaiter().GetResult();

            bool hasCallbackResult = false;
            WebRequestResult<string> callbackResult = default;
            _manager.GetWithResult<string>(url, result =>
            {
                callbackResult = result;
                hasCallbackResult = true;
            });
            yield return RunUntil(() => hasCallbackResult, "GetWithResult answered");

            Assert.IsFalse(asyncResult.IsSuccess);
            Assert.AreEqual(0, asyncResult.StatusCode, "A request that got no response has no HTTP status.");
            Assert.IsTrue(asyncResult.ResponseHeaders == null || asyncResult.ResponseHeaders.Count == 0, "A request that got no response has no headers.");
            Assert.AreEqual(callbackResult.StatusCode, asyncResult.StatusCode);
        }

        [UnityTest]
        public IEnumerator GetResultAsync_HttpErrorWithRetries_RetriesThenKeepsTheStatusCode()
        {
            _server = new TestHttpServer(500);
            LogAssert.Expect(LogType.Error, new Regex("WebRequest GET failed"));

            UniTask<WebRequestResult<string>> request = _manager.GetResultAsync<string>(_server.Url, retryCount: 2, retryDelaySeconds: 0.0f);
            yield return RunUntil(() => request.Status != UniTaskStatus.Pending, "GetResultAsync completed");
            WebRequestResult<string> result = request.GetAwaiter().GetResult();

            Assert.AreEqual(3, _server.RequestCount, "The first attempt and two retries reach the server.");
            Assert.AreEqual(500, result.StatusCode);
            Assert.IsTrue(result.ResponseHeaders.ContainsKey(HeaderName));
        }

        [UnityTest]
        public IEnumerator GetResultAsync_Canceled_AnswersWithACanceledFailure()
        {
            _server = new TestHttpServer(200, holdResponse: true);
            CancellationTokenSource cancellation = new CancellationTokenSource();

            UniTask<WebRequestResult<string>> request = _manager.GetResultAsync<string>(_server.Url, cancellationToken: cancellation.Token);
            yield return RunUntil(() => _server.RequestCount > 0, "the request reached the server");
            cancellation.Cancel();
            yield return RunUntil(() => request.Status != UniTaskStatus.Pending, "GetResultAsync completed");
            WebRequestResult<string> result = request.GetAwaiter().GetResult();
            cancellation.Dispose();

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(-1, result.StatusCode);
            StringAssert.Contains("canceled", result.ErrorMessage);
        }

        [UnityTest]
        public IEnumerator GetResultAsync_CanceledWithPropagation_ThrowsOperationCanceled()
        {
            _server = new TestHttpServer(200, holdResponse: true);
            CancellationTokenSource cancellation = new CancellationTokenSource();

            UniTask<WebRequestResult<string>> request = _manager.GetResultAsync<string>(
                _server.Url,
                cancellationToken: cancellation.Token,
                propagateCancellation: true);
            yield return RunUntil(() => _server.RequestCount > 0, "the request reached the server");
            cancellation.Cancel();
            yield return RunUntil(() => request.Status != UniTaskStatus.Pending, "GetResultAsync completed");
            cancellation.Dispose();

            Assert.AreEqual(UniTaskStatus.Canceled, request.Status);
            Assert.Catch<OperationCanceledException>(() => request.GetAwaiter().GetResult());
        }
    }
}
#endif
