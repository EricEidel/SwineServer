using System;
using System.Net;
using vtortola.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace SwinecideServer
{
    public class SwinecideServer
    {
        CancellationTokenSource cancellation;
        WebSocketListener server;

        public void Start()
        {
            cancellation = new CancellationTokenSource();
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("192.168.0.14"), 8005);
            server = new WebSocketListener(endpoint);
            var rfc6455 = new vtortola.WebSockets.Rfc6455.WebSocketFactoryRfc6455(server);
            server.Standards.RegisterStandard(rfc6455);
            server.Start();

            Console.WriteLine("Echo Server started at " + endpoint.ToString());

            var task = Task.Run(() => AcceptWebSocketClientsAsync(server, cancellation.Token));
        }

        public void Stop()
        {
            cancellation.Cancel();
            if (server != null)
            {
                server.Stop();
            }
        }

        async Task AcceptWebSocketClientsAsync(WebSocketListener server, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var ws = await server.AcceptWebSocketAsync(token).ConfigureAwait(false);
                    if (ws != null)
                        Task.Run(() => HandleConnectionAsync(ws, token));
                }
                catch (Exception aex)
                {
                    Console.WriteLine("Error Accepting clients: " + aex.GetBaseException().Message);
                }
            }
            Console.WriteLine("Server Stop accepting clients");
        }

        async Task HandleConnectionAsync(WebSocket ws, CancellationToken cancellation)
        {
            try
            {
                while (ws.IsConnected && !cancellation.IsCancellationRequested)
                {
                    String msg = await ws.ReadStringAsync(cancellation).ConfigureAwait(false);
                    if (msg != null)
                    {
                        Console.WriteLine("Message from client was: " + msg);
                        ws.WriteString("Message from server is: " + msg);
                    }
                }
            }
            catch (Exception aex)
            {
                Console.WriteLine("Error Handling connection: " + aex.GetBaseException().Message);
                try { ws.Close(); }
                catch { }
            }
            finally
            {
                ws.Dispose();
            }
        }
    }
}
