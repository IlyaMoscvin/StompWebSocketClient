using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NikitaTrubkin.StompClient;
using syp.biz.SockJS.NET.Common.Extensions;
using syp.biz.SockJS.NET.Common.Interfaces;
using UnityEngine;
using ILogger = syp.biz.SockJS.NET.Common.Interfaces.ILogger;

namespace syp.biz.SockJS.NET.Client.Implementations.Transports
{
    [SuppressMessage("ReSharper", "UnusedType.Global")]
    internal class SystemWebSocketTransportFactory : ITransportFactory
    {
        
        #region Implementation of ITransportFactory

        public string Name => "websocket-system";
        public bool Enabled { get; set; } = CheckIfWebSocketIsSupported();
        public uint Priority { get; set; } = 100;

        public Task<ITransport> Build(ITransportConfiguration config)
        {
            config.Logger.Debug($"{nameof(this.Build)}: '{this.Name}' transport");
            var transport = new SystemWebSocketTransport(config);
            return Task.FromResult<ITransport>(transport);
        }

        #endregion Implementation of ITransportFactory

        private static bool CheckIfWebSocketIsSupported()
        {
            try
            {
                using var socket = new ClientWebSocket();
                return true;
            }
            catch (PlatformNotSupportedException)
            {
                return false;
            }
        }
    }

    internal class SystemWebSocketTransport : ITransport
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly ITransportConfiguration _config;
        private readonly ILogger _log;
        private readonly ClientWebSocket _socket;
        private readonly StompMessageSerializer stompSerializer = new StompMessageSerializer();

        public SystemWebSocketTransport(ITransportConfiguration config)
        {
            this._config = config;
            this._log = config.Logger;
            this._socket = new ClientWebSocket();
            this.Configure(this._socket.Options, config);
        }

        #region Implementation of IDisposable

        public void Dispose()
        {
            this._cts.Dispose();
            this._socket.Dispose();
        }

        #endregion Implementation of IDisposable

        #region Implementation of ITransport

        public event EventHandler<string>? Message;
        public event EventHandler? Disconnected;

        public Dictionary<string, string> GetHeaders()
        {
            var headers = _config.DefaultRequestHeaders;
            return headers.Keys.Cast<string>().ToDictionary(header => header, header => headers[header]);
        }
        public async Task Connect(CancellationToken token, string userToken)
        {
            var endpoint = this.BuildEndpoint();
            Debug.Log($"{nameof(this.Connect)}: {endpoint}");

            await this._socket.ConnectAsync(endpoint, token);
            _ = Task.Factory.StartNew(this.ReceiveLoop, this._cts, TaskCreationOptions.LongRunning)
                .ConfigureAwait(false);
            Dictionary<string, string> headers = new()
            {
                {"Authorization","Bearer " + userToken},
                {"accept-version", "1.1,1.0"},
                {"heart-beat", "10000,10000"}
            };
            //config.DefaultHeaders?.Add(HttpRequestHeader.Authorization, "Bearer " + Token);
            //config.DefaultHeaders?.Add("accept-version", "1.1,1.0");
            //config.DefaultHeaders?.Add("heart-beat", "10000,10000");
            var message = new StompMessage(StompCommand.Connect, headers);
            var buffer = Encoding.UTF8.GetBytes(stompSerializer.Serialize(message)).AsMemory();
            Debug.Log(stompSerializer.Serialize(message));
            await _socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public Task Connect(string userToken) => this.Connect(CancellationToken.None, userToken);

        public async Task Disconnect()
        {
            this._log.Info(nameof(this.Disconnect));
            if (this._socket.State == WebSocketState.Closed) return;
            await this._socket.CloseAsync(WebSocketCloseStatus.Empty, "", this._cts.Token);
            this.Disconnected?.Invoke(this, EventArgs.Empty);
        }

        public Task Send(string data) => this.Send(data, CancellationToken.None);

        public async Task Send(string data, CancellationToken token)
        {
            this._log.Debug($"{nameof(this.Send)}: {data}");
            if (data is null) return;

            this.VerifyOpen();
            var buffer = Encoding.UTF8.GetBytes(data).AsMemory();
            await this._socket.SendAsync(buffer, WebSocketMessageType.Text, true, token);
        }

        #endregion Implementation of ITransport

        public async void Subscribe<T>(string topic, IDictionary<string, string> headers, EventHandler<T> handler)
        {
            if (_socket.State != WebSocketState.Open)
                throw new InvalidOperationException("The current state of the connection is not Open.");
            
            headers.Add("id", "sub-0"); // todo: study and implement
            headers.Add("destination", topic);
            var subscribeMessage = new StompMessage(StompCommand.Subscribe, headers);
            var buffer = Encoding.UTF8.GetBytes(stompSerializer.Serialize(subscribeMessage)).AsMemory();
            await _socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
            Debug.Log("Subscribed");
            Debug.Log(stompSerializer.Serialize(subscribeMessage));
            // todo: check response
            // todo: implement advanced topic
            //var sub = new Subscriber((sender, body) => handler(this, (T)body), typeof(T));
            //subscribers.Add(topic, sub);
        }

        public async Task Send(string topic, string data, IDictionary<string, string> headers)
        {
            if (_socket.State != WebSocketState.Open)
                throw new InvalidOperationException("The current state of the connection is not Open.");

            headers.Add("destination", topic);
            //headers.Add("id", "stub"); // todo: study and implement
            var message = new StompMessage(StompCommand.Send, data, headers);
            var buffer = Encoding.UTF8.GetBytes(stompSerializer.Serialize(message)).AsMemory();
            await _socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private Uri BuildEndpoint()
        {
            var endpoint = new UriBuilder(this._config.BaseEndpoint);
            endpoint.Scheme = endpoint.Scheme == "https" ? "wss" : "ws";
            //endpoint.AddPath("/websocket");
            return endpoint.Uri;
        }

        private async void ReceiveLoop(object obj)
        {
            try
            {
                this._log.Debug(nameof(this.ReceiveLoop));

                var builder = new StringBuilder();
                var buffer = new ArraySegment<byte>(new byte[1024]);

                while (!this._cts.IsCancellationRequested && this._socket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await this._socket.ReceiveAsync(buffer, this._cts.Token);
                        var data = Encoding.UTF8.GetString(buffer.AsSpan(buffer.Offset, result.Count));
                        builder.Append(data);
                    } while (!result.EndOfMessage);

                    var message = builder.ToString();
                    builder.Clear();

                    if (!message.IsNullOrWhiteSpace())
                        Task.Run(() => this.Message?.Invoke(this, message)).IgnoreAwait();
                    if (result.MessageType != WebSocketMessageType.Close) continue;

                    this._log.Error("Server sent close message");
                    await this.Disconnect();
                    return;
                }
            }
            catch (Exception ex)
            {
                this._log.Error($"{nameof(this.ReceiveLoop)}: {ex}");
                await this.Disconnect();
            }
        }

        private void VerifyOpen()
        {
            var state = this._socket.State;
            if (state != WebSocketState.Open) throw new Exception($"Invalid socket state '{state}");
        }

        private void Configure(ClientWebSocketOptions options, ITransportConfiguration config)
        {
            config.DefaultRequestHeaders.AsEnumerable().ForEach(i => options.SetRequestHeader(i.name, i.value));
            if (!(config.Proxy is null)) options.Proxy = config.Proxy;
            if (!(config.Cookies is null)) options.Cookies = config.Cookies;
            if (!(config.RemoteCertificateValidator is null))
                options.RemoteCertificateValidationCallback = config.RemoteCertificateValidator;
            if (!(config.ClientCertificates is null)) options.ClientCertificates = config.ClientCertificates;
            if (config.KeepAliveInterval.HasValue) options.KeepAliveInterval = config.KeepAliveInterval.Value;
            if (config.Credentials is null) return;
            options.Credentials = config.Credentials;
            options.UseDefaultCredentials = false;
        }
    }
}