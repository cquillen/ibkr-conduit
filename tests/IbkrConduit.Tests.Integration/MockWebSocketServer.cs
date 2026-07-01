using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace IbkrConduit.Tests.Integration;

/// <summary>
/// Minimal in-process WebSocket server used as a hermetic stand-in for the IBKR
/// streaming endpoint. The eager <c>IIbkrClientManager.AddAsync</c> flow connects a
/// real <see cref="System.Net.WebSockets.ClientWebSocket"/>, so tests point
/// <c>IbkrClientOptions.WebSocketBaseUrl</c> at this server's <see cref="Url"/> instead
/// of production IBKR.
/// </summary>
/// <remarks>
/// Behavior: completes the WebSocket handshake so the client's <c>ConnectAsync</c>
/// returns; accepts many concurrent connections; drains and ignores all inbound frames
/// (e.g. the client's "tic" heartbeats) without echoing, so the client's message pump
/// blocks harmlessly instead of erroring into a reconnect loop; and responds to a
/// client-initiated close so teardown is graceful. It never originates messages.
/// </remarks>
internal sealed class MockWebSocketServer : IAsyncDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _acceptLoop;
    private readonly ConcurrentDictionary<Task, byte> _connections = new();
    private int _connectionCount;

    private MockWebSocketServer(HttpListener listener, int port)
    {
        _listener = listener;
        Url = $"ws://127.0.0.1:{port}/v1/api/ws";
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    /// <summary>The <c>ws://</c> base URL to assign to <c>IbkrClientOptions.WebSocketBaseUrl</c>.</summary>
    public string Url { get; }

    /// <summary>Total number of WebSocket connections that completed the handshake.</summary>
    public int ConnectionCount => Volatile.Read(ref _connectionCount);

    /// <summary>Starts the server on a free loopback port.</summary>
    public static MockWebSocketServer Start()
    {
        var port = GetFreeTcpPort();
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        return new MockWebSocketServer(listener, port);
    }

    private static int GetFreeTcpPort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        try
        {
            return ((IPEndPoint)probe.LocalEndpoint).Port;
        }
        finally
        {
            probe.Stop();
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception)
            {
                // Listener stopped/disposed — exit the loop.
                break;
            }

            if (!context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
                continue;
            }

            var handler = Task.Run(() => HandleConnectionAsync(context, ct));
            _connections.TryAdd(handler, 0);
            _ = handler.ContinueWith(t => _connections.TryRemove(t, out _), TaskScheduler.Default);
        }
    }

    private async Task HandleConnectionAsync(HttpListenerContext context, CancellationToken ct)
    {
        HttpListenerWebSocketContext wsContext;
        try
        {
            wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
        }
        catch (Exception)
        {
            context.Response.Abort();
            return;
        }

        Interlocked.Increment(ref _connectionCount);
        var ws = wsContext.WebSocket;
        var buffer = new byte[4096];
        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(buffer.AsMemory(), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "ack", CancellationToken.None);
                    break;
                }

                // Drain and ignore inbound frames (heartbeats, subscriptions). Never echo.
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        catch (WebSocketException)
        {
            // Client vanished — nothing to clean up beyond disposal below.
        }
        finally
        {
            ws.Dispose();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch (Exception)
        {
            // Best-effort teardown.
        }

        try
        {
            await _acceptLoop;
        }
        catch (Exception)
        {
            // Accept loop unwinds via the cancelled listener.
        }

        // Drain in-flight connection handlers so DisposeAsync is fully quiesced.
        // Task.WhenAll of an empty array is a no-op, so the normal path is unchanged.
        try
        {
            await Task.WhenAll(_connections.Keys.ToArray());
        }
        catch (Exception)
        {
            // Connections cancelled via the CT surface OperationCanceledException — expected on shutdown.
        }

        _cts.Dispose();
    }
}
