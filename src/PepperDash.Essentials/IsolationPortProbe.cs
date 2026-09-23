using System;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;
using PepperDash.Core;
using Serilog.Events;

namespace PepperDash.Essentials
{
    /// <summary>
    /// Opens a bare TCP listener through Crestron's own socket API, so that a port's reachability
    /// from the LAN can be tested by hand.
    /// </summary>
    /// <remarks>
    /// A processor in isolation mode refuses a connection from the LAN to the Mobile Control direct
    /// server, although Crestron's firewall table for isolation mode admits "listen ports used by
    /// program". The difference we can act on is that the direct server is WebSocketSharp on a plain
    /// .NET TcpListener, while the firmware's firewall rules appear to follow the sockets opened
    /// through its own API. This probe opens a port the second way and nothing else, which tells the
    /// two explanations apart: if the probe's port answers from the LAN under isolation and the
    /// direct server's does not, the socket API is what earns the firewall rule.
    /// </remarks>
    public static class IsolationPortProbe
    {
        private const int BufferSize = 1024;

        private static readonly byte[] Banner = System.Text.Encoding.ASCII.GetBytes("ISOLATION PROBE OK\r\n");

        private static TCPServer _server;

        private static int _port;

        /// <summary>
        /// Console handler: a port number starts the probe, "stop" ends it, nothing reports state.
        /// </summary>
        public static void Run(string args)
        {
            var argument = (args ?? string.Empty).Trim();

            if (argument.Length == 0)
            {
                Respond(_server == null
                    ? "Isolation probe is not running. Start it with: isolationprobe <port>"
                    : string.Format("Isolation probe is listening on port {0}, status {1}, {2} client(s) connected",
                        _port, _server.State, _server.NumberOfClientsConnected));
                return;
            }

            if (argument.Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                Stop();
                return;
            }

            if (!int.TryParse(argument, out var port) || port < 1 || port > 65535)
            {
                Respond(string.Format("'{0}' is not a port number. Usage: isolationprobe <port> | stop", argument));
                return;
            }

            Start(port);
        }

        private static void Start(int port)
        {
            Stop();

            try
            {
                _port = port;

                // 0.0.0.0 accepts from any address, on every adapter
                _server = new TCPServer("0.0.0.0", port, BufferSize);

                var result = _server.WaitForConnectionsAlways(OnClientConnected);

                Respond(string.Format("Isolation probe listening on port {0}: {1}. Connect to it from the LAN, " +
                    "for example: curl -v -m 5 http://<processor>:{0}/", port, result));

                Debug.LogMessage(LogEventLevel.Information, "Isolation probe listening on port {port}: {result}", null, port, result);
            }
            catch (Exception ex)
            {
                _server = null;
                Respond(string.Format("Isolation probe could not open port {0}: {1}", port, ex.Message));
            }
        }

        private static void Stop()
        {
            if (_server == null) return;

            try
            {
                _server.DisconnectAll();
                _server.Stop();
                Respond(string.Format("Isolation probe on port {0} stopped", _port));
            }
            catch (Exception ex)
            {
                Respond(string.Format("Isolation probe on port {0} did not stop cleanly: {1}", _port, ex.Message));
            }
            finally
            {
                _server = null;
            }
        }

        private static void OnClientConnected(TCPServer server, uint clientIndex)
        {
            if (clientIndex == 0) return;

            var client = server.GetAddressServerAcceptedConnectionFromForSpecificClient(clientIndex);

            Debug.LogMessage(LogEventLevel.Information, "Isolation probe on port {port} accepted a connection from {client}", null, _port, client);

            server.SendData(clientIndex, Banner, Banner.Length);
        }

        private static void Respond(string message)
        {
            CrestronConsole.ConsoleCommandResponse(message + CrestronEnvironment.NewLine);
        }
    }
}
