using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;

// Crestron.SimplSharp has types of these names too, and the sockets here are .NET ones
using IPAddress = System.Net.IPAddress;
using IPEndPoint = System.Net.IPEndPoint;
using PepperDash.Core;
using PepperDash.Core.Logging;

namespace PepperDash.Essentials.WebSocketServer
{
    /// <summary>
    /// Carries the direct server's traffic over a socket opened through Crestron's own API, so that a
    /// processor in isolation mode admits it.
    /// </summary>
    /// <remarks>
    /// A 4-Series processor with a Control Subnet in isolation mode puts a firewall in front of the
    /// CPU. Crestron's table for that firewall admits "listen ports used by program", but what earns
    /// a rule is a socket opened through their socket API: the direct server is WebSocketSharp on a
    /// plain .NET TcpListener, which the firmware never learns about, and the port is refused from
    /// the LAN. Measured on a CP4N, 2026-09-23: with isolation on, a bare Crestron TCPServer answered
    /// from the LAN while the direct server's own port was refused, on the same boot from the same
    /// machine.
    ///
    /// So this listens on the public port with a Crestron TCPServer and hands each connection to the
    /// direct server over the loopback interface. It relays bytes and reads none of them, which keeps
    /// HTTP, WebSocket framing and TLS alike untouched.
    ///
    /// The one thing a relay destroys is the client's address - everything arrives at the server from
    /// 127.0.0.1 - and the server picks which application config to serve by whether the client is on
    /// the Control Subnet. Each loopback connection's local port is therefore recorded against the
    /// address it carries, and the server resolves a loopback peer back through
    /// <see cref="TryGetClientAddress"/>.
    /// </remarks>
    public class CrestronSocketRelay : IKeyed
    {
        /// <summary>Per-connection copy buffer. Large enough that the app bundle does not crawl.</summary>
        private const int BufferSize = 16384;

        /// <summary>
        /// Browsers open several connections per page - six is common - and every panel, wrapper app
        /// and browser client on the system shares this listener.
        /// </summary>
        private const int DefaultMaxConnections = 64;

        /// <summary>Connected-client count at which the log starts warning that slots are running out.</summary>
        private const double BusyFraction = 0.75;

        private readonly int _publicPort;
        private readonly int _loopbackPort;
        private readonly int _maxConnections;

        /// <summary>
        /// The adapter this relay listens on. Measured on a CP4N, the Crestron socket takes the whole
        /// port whichever adapter it is given, so this does not free the number for anything else.
        /// </summary>
        private readonly EthernetAdapterType _adapter;

        /// <summary>
        /// The address the direct server listens on behind the relay. Loopback unless the platform
        /// turns out to refuse it, in which case one of the processor's own addresses works as well -
        /// the port is not reachable from outside either way, because the firewall never opened it.
        /// </summary>
        private readonly IPAddress _loopbackAddress;

        /// <summary>Loopback source port to the address of the client it carries. See the class remarks.</summary>
        private static readonly Dictionary<int, IPAddress> ClientAddressByLoopbackPort = new Dictionary<int, IPAddress>();

        private static readonly object ClientAddressLock = new object();

        private readonly Dictionary<uint, Connection> _connections = new Dictionary<uint, Connection>();

        private readonly object _connectionsLock = new object();

        private TCPServer _server;

        /// <summary>Connections accepted since the relay opened - see the log in OnClientConnected.</summary>
        private int _accepted;

        /// <summary>How many accepted connections are logged at information level before it quietens.</summary>
        private const int AcceptsToLogPlainly = 5;

        /// <summary>How much of a stream's first bytes the log shows - enough for an HTTP start line.</summary>
        private const int PreviewLength = 120;

        /// <inheritdoc />
        public string Key { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="key">device key</param>
        /// <param name="publicPort">the port clients connect to</param>
        /// <param name="adapter">the adapter to listen on</param>
        /// <param name="loopbackAddress">the address the direct server listens on, null for loopback</param>
        /// <param name="loopbackPort">the port the direct server listens on behind the relay</param>
        /// <param name="maxConnections">simultaneous connections to allow, 0 for the default</param>
        public CrestronSocketRelay(string key, int publicPort, EthernetAdapterType adapter, IPAddress loopbackAddress, int loopbackPort, int maxConnections)
        {
            Key = key;
            _publicPort = publicPort;
            _adapter = adapter;
            _loopbackAddress = loopbackAddress ?? IPAddress.Loopback;
            _loopbackPort = loopbackPort;
            _maxConnections = maxConnections > 0 ? maxConnections : DefaultMaxConnections;
        }

        /// <summary>
        /// The address of the client behind a loopback connection, for a server that would otherwise
        /// see only 127.0.0.1. False when the endpoint is not one of ours, in which case it is a real
        /// client address already.
        /// </summary>
        public static bool TryGetClientAddress(IPEndPoint loopbackEndPoint, out IPAddress clientAddress)
        {
            clientAddress = null;

            if (loopbackEndPoint == null || !IPAddress.IsLoopback(loopbackEndPoint.Address))
            {
                return false;
            }

            lock (ClientAddressLock)
            {
                return ClientAddressByLoopbackPort.TryGetValue(loopbackEndPoint.Port, out clientAddress);
            }
        }

        /// <summary>
        /// Opens the public port. Safe to call more than once.
        /// </summary>
        public void Start()
        {
            Stop();

            try
            {
                _server = new TCPServer("0.0.0.0", _publicPort, BufferSize, _adapter, _maxConnections)
                {
                    // Every read has to start at the beginning of the buffer. Left off, received
                    // data accumulates and a read hands back bytes that were already relayed, which
                    // corrupts anything with a length or a frame - which is to say all of it.
                    ClearIncomingDataBuffer = true,
                };

                var result = _server.WaitForConnectionsAlways(OnClientConnected);

                this.LogInformation(
                    "Relaying {adapter} port {publicPort} to the direct server on {loopbackAddress}:{loopbackPort}, up to {maxConnections} connections: {result}",
                    _adapter, _publicPort, _loopbackAddress, _loopbackPort, _maxConnections, result);

                // An async accept reports itself as pending, which is this call working as intended;
                // only anything else is a failure to open the port.
                if (result != SocketErrorCodes.SOCKET_OK && result != SocketErrorCodes.SOCKET_OPERATION_PENDING)
                {
                    this.LogError("Port {publicPort} did not open: {result}. Clients cannot reach the direct server.",
                        _publicPort, result);
                    return;
                }

                CheckLoopbackLeg();
            }
            catch (Exception ex)
            {
                _server = null;
                this.LogError(ex, "Could not open port {publicPort} for the direct server", _publicPort);
            }
        }

        /// <summary>
        /// Dials the direct server the same way a relayed connection will, and says whether it
        /// answered.
        /// </summary>
        /// <remarks>
        /// The two halves fail identically from a client's point of view - the connection does not
        /// work - so the startup log has to separate them. This one line says whether the half that
        /// cannot be tested from outside the processor is sound.
        /// </remarks>
        private void CheckLoopbackLeg()
        {
            try
            {
                using (var probe = new TcpClient())
                {
                    probe.Connect(_loopbackAddress, _loopbackPort);

                    this.LogInformation("Direct server answered on {address}:{port}; the relay's inner leg is good",
                        _loopbackAddress, _loopbackPort);
                }
            }
            catch (Exception ex)
            {
                this.LogError(ex,
                    "Direct server did not answer on {address}:{port}. The public port is open but nothing can be " +
                    "carried to the server through it. If this platform refuses loopback, set " +
                    "directServer.relayLoopbackAddress to one of the processor's own addresses.",
                    _loopbackAddress, _loopbackPort);
            }
        }

        /// <summary>
        /// Closes the public port and every connection running through it.
        /// </summary>
        public void Stop()
        {
            if (_server == null) return;

            List<Connection> connections;

            lock (_connectionsLock)
            {
                connections = new List<Connection>(_connections.Values);
                _connections.Clear();
            }

            foreach (var connection in connections)
            {
                connection.Close();
            }

            try
            {
                _server.DisconnectAll();
                _server.Stop();
            }
            catch (Exception ex)
            {
                this.LogDebug(ex, "Error closing the relay on port {publicPort}", _publicPort);
            }
            finally
            {
                _server = null;
            }
        }

        /// <summary>
        /// One line describing what the relay is doing, for the console command and the log.
        /// </summary>
        public string Describe()
        {
            if (_server == null)
            {
                return string.Format("Relay on port {0} is not running", _publicPort);
            }

            return string.Format(
                "Relay on {0}: port {1} -> {2}:{3}, status {4}, {5} of {6} connections in use, {7} accepted since start",
                _adapter, _publicPort, _loopbackAddress, _loopbackPort, _server.ServerSocketStatus,
                _server.NumberOfClientsConnected, _maxConnections, _accepted);
        }

        private void OnClientConnected(TCPServer server, uint clientIndex)
        {
            if (clientIndex == 0) return;

            var clientAddressText = server.GetAddressServerAcceptedConnectionFromForSpecificClient(clientIndex);

            try
            {
                IPAddress.TryParse(clientAddressText, out var clientAddress);

                var connection = new Connection(this, server, clientIndex, clientAddress);

                lock (_connectionsLock)
                {
                    _connections[clientIndex] = connection;
                }

                connection.Open(_loopbackAddress, _loopbackPort);

                var connected = server.NumberOfClientsConnected;

                // The first few are logged plainly: they are the proof that the relay carries traffic
                // at all, and nobody turning a system up should have to raise the log level for it.
                if (_accepted++ < AcceptsToLogPlainly)
                {
                    this.LogInformation("Relaying a connection from {clientAddress}, {connected} of {maxConnections} in use",
                        clientAddressText, connected, _maxConnections);
                }
                else
                {
                    this.LogVerbose("Relaying a connection from {clientAddress}, {connected} of {maxConnections} in use",
                        clientAddressText, connected, _maxConnections);
                }

                if (connected >= _maxConnections * BusyFraction)
                {
                    this.LogWarning(
                        "{connected} of {maxConnections} relay connections are in use. Beyond the limit, clients are dropped as they connect and the app loads only in part.",
                        connected, _maxConnections);
                }
            }
            catch (Exception ex)
            {
                this.LogError(ex, "Could not relay the connection from {clientAddress}", clientAddressText);
                EndConnection(clientIndex);
            }
        }

        /// <summary>
        /// Drops a connection and forgets it, whichever end closed first.
        /// </summary>
        private void EndConnection(uint clientIndex)
        {
            Connection connection = null;

            lock (_connectionsLock)
            {
                if (_connections.TryGetValue(clientIndex, out connection))
                {
                    _connections.Remove(clientIndex);
                }
            }

            connection?.Close();
        }

        /// <summary>
        /// One client: the Crestron socket it arrived on, and the loopback socket carrying it to the
        /// direct server. Copies in both directions until either end closes, then closes the other.
        /// </summary>
        private class Connection
        {
            private readonly CrestronSocketRelay _relay;
            private readonly TCPServer _server;
            private readonly uint _clientIndex;
            private readonly IPAddress _clientAddress;
            private readonly byte[] _fromServerBuffer = new byte[BufferSize];

            private TcpClient _loopback;
            private NetworkStream _stream;
            private int _loopbackPort;
            private bool _closed;

            private long _fromClientBytes;
            private long _toClientBytes;

            /// <summary>Why this connection ended, for the log line when it does.</summary>
            private string _endedBecause = "not ended";

            /// <summary>
            /// Whether this connection reports what it carries. The first connection after the relay
            /// opens does, which is enough to see a request go out and a response come back without
            /// filling the log with every asset the app fetches.
            /// </summary>
            private bool Narrates => _relay._accepted <= 1;

            /// <summary>
            /// Points each request's Host header at the server's port - see HttpHostRewriter. Null when
            /// the two ports are the same and there is nothing to rewrite.
            /// </summary>
            private readonly HttpHostRewriter _rewriter;

            public Connection(CrestronSocketRelay relay, TCPServer server, uint clientIndex, IPAddress clientAddress)
            {
                _relay = relay;
                _server = server;
                _clientIndex = clientIndex;
                _clientAddress = clientAddress;

                if (relay._publicPort != relay._loopbackPort)
                {
                    _rewriter = new HttpHostRewriter(relay._publicPort, relay._loopbackPort);
                }
            }

            public void Open(IPAddress loopbackAddress, int loopbackPort)
            {
                _loopback = new TcpClient();
                _loopback.Connect(loopbackAddress, loopbackPort);
                _loopback.NoDelay = true;
                _stream = _loopback.GetStream();

                // Record which client this loopback connection carries before any of its bytes reach
                // the server, so the server never sees a request it cannot attribute.
                if (_clientAddress != null && _loopback.Client.LocalEndPoint is IPEndPoint localEndPoint)
                {
                    _loopbackPort = localEndPoint.Port;

                    lock (ClientAddressLock)
                    {
                        ClientAddressByLoopbackPort[_loopbackPort] = _clientAddress;
                    }
                }

                ReadFromClient();
                ReadFromServer();
            }

            /// <summary>Client to direct server.</summary>
            private void ReadFromClient()
            {
                if (_closed) return;

                _server.ReceiveDataAsync(_clientIndex, (server, clientIndex, bytesReceived) =>
                {
                    if (bytesReceived <= 0)
                    {
                        _endedBecause = "the client closed";
                        _relay.EndConnection(_clientIndex);
                        return;
                    }

                    try
                    {
                        var buffer = server.GetIncomingDataBufferForSpecificClient(clientIndex);

                        if (_rewriter == null)
                        {
                            _stream.Write(buffer, 0, bytesReceived);
                        }
                        else
                        {
                            // may hold bytes back while a header block is still arriving
                            var forward = _rewriter.Process(buffer, 0, bytesReceived);

                            if (forward.Length > 0)
                            {
                                _stream.Write(forward, 0, forward.Length);
                            }
                        }

                        _fromClientBytes += bytesReceived;

                        if (Narrates && _fromClientBytes == bytesReceived)
                        {
                            _relay.LogInformation("First {bytes} bytes from the client: {preview}",
                                bytesReceived, Preview(buffer, bytesReceived));
                        }

                        ReadFromClient();
                    }
                    catch (Exception ex)
                    {
                        _endedBecause = "writing to the server failed: " + ex.Message;
                        _relay.EndConnection(_clientIndex);
                    }
                });
            }

            /// <summary>Direct server to client.</summary>
            private void ReadFromServer()
            {
                if (_closed) return;

                try
                {
                    _stream.BeginRead(_fromServerBuffer, 0, _fromServerBuffer.Length, OnServerData, null);
                }
                catch (Exception)
                {
                    _relay.EndConnection(_clientIndex);
                }
            }

            private void OnServerData(IAsyncResult result)
            {
                try
                {
                    var bytesRead = _stream.EndRead(result);

                    if (bytesRead <= 0)
                    {
                        _endedBecause = "the server closed";
                        _relay.EndConnection(_clientIndex);
                        return;
                    }

                    var sent = _server.SendData(_clientIndex, _fromServerBuffer, bytesRead);

                    _toClientBytes += bytesRead;

                    if (Narrates && _toClientBytes == bytesRead)
                    {
                        _relay.LogInformation("First {bytes} bytes from the server: {preview}",
                            bytesRead, Preview(_fromServerBuffer, bytesRead));
                    }

                    if (sent != SocketErrorCodes.SOCKET_OK && sent != SocketErrorCodes.SOCKET_OPERATION_PENDING)
                    {
                        _relay.LogWarning("Sending {bytes} bytes to the client returned {sent}", bytesRead, sent);
                    }

                    ReadFromServer();
                }
                catch (Exception ex)
                {
                    // either end closing lands here; closing both is the only correct response
                    _endedBecause = "reading from the server failed: " + ex.Message;
                    _relay.EndConnection(_clientIndex);
                }
            }

            /// <summary>The first bytes of a stream as text, for telling a request from a response.</summary>
            private static string Preview(byte[] buffer, int length)
            {
                var text = System.Text.Encoding.ASCII.GetString(buffer, 0, Math.Min(length, PreviewLength));
                var readable = new System.Text.StringBuilder(text.Length);

                foreach (var character in text)
                {
                    // line breaks and anything else unprintable become dots, so a start line stays on one log line
                    readable.Append(character < ' ' || character > '~' ? '.' : character);
                }

                return readable.ToString();
            }

            public void Close()
            {
                if (_closed) return;

                _closed = true;

                _relay.LogInformation(
                    "Connection ended after {fromClientBytes} bytes in and {toClientBytes} bytes out, {requests} request(s) routed{handover}: {reason}",
                    _fromClientBytes, _toClientBytes, _rewriter?.RewrittenRequests ?? 0,
                    _rewriter?.PassThroughReason == null ? "" : " then passed through for " + _rewriter.PassThroughReason,
                    _endedBecause);

                if (_loopbackPort != 0)
                {
                    lock (ClientAddressLock)
                    {
                        ClientAddressByLoopbackPort.Remove(_loopbackPort);
                    }
                }

                try { _stream?.Close(); } catch (Exception) { }
                try { _loopback?.Close(); } catch (Exception) { }
                try { _server.Disconnect(_clientIndex); } catch (Exception) { }
            }
        }
    }
}
