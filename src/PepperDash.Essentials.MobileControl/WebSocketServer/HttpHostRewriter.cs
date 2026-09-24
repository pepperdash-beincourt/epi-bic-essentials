using System;
using System.IO;
using System.Text;

namespace PepperDash.Essentials.WebSocketServer
{
    /// <summary>
    /// Rewrites the port in the Host header of every HTTP request in a client-to-server byte stream,
    /// and passes everything else through untouched.
    /// </summary>
    /// <remarks>
    /// websocket-sharp matches a request to a listener by the port in its Host header - for an IP host
    /// it compares nothing else - and a client's header names the port it dialled. Behind the relay
    /// the server listens on a different port from the one clients dial, because this platform's
    /// Crestron socket takes the whole port whatever adapter it is given, so both cannot hold it.
    /// Unrewritten, every request matches no listener and is answered with an error.
    ///
    /// The stream is followed as HTTP rather than searched for a pattern, so a header split across
    /// two reads, several requests on one keep-alive connection, and request bodies are all handled:
    /// headers are collected up to their blank line and rewritten whole, a body is counted through by
    /// its Content-Length, and a WebSocket upgrade hands the rest of the connection over as frames,
    /// which are never examined. Anything it cannot follow - a chunked request body, or a header block
    /// past <see cref="MaxHeaderBytes"/> - switches the connection to passing through unchanged, which
    /// is exactly what it did before this existed.
    ///
    /// Deliberately free of Crestron types, so it can be exercised off the processor.
    /// </remarks>
    public sealed class HttpHostRewriter
    {
        /// <summary>Largest header block followed before giving up and passing through.</summary>
        public const int MaxHeaderBytes = 65536;

        // ISO-8859-1 maps every byte to the character of the same value and back, so a header block
        // round-trips through a string without changing a single byte it does not mean to.
        private static readonly Encoding Latin1 = Encoding.GetEncoding(28591);

        private enum Mode
        {
            Headers,
            Body,
            PassThrough,
        }

        private readonly string _fromSuffix;
        private readonly string _toSuffix;
        private readonly MemoryStream _header = new MemoryStream();

        private Mode _mode = Mode.Headers;
        private long _bodyRemaining;

        /// <summary>
        /// Requests whose Host header was rewritten, for diagnostics.
        /// </summary>
        public int RewrittenRequests { get; private set; }

        /// <summary>
        /// Why the connection stopped being followed as HTTP, or null while it still is.
        /// </summary>
        public string PassThroughReason { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="fromPort">the port clients put in their Host header</param>
        /// <param name="toPort">the port the server listens on</param>
        public HttpHostRewriter(int fromPort, int toPort)
        {
            _fromSuffix = ":" + fromPort;
            _toSuffix = ":" + toPort;
        }

        /// <summary>
        /// Takes the next bytes from the client and returns what to forward to the server. May return
        /// nothing while a header block is still arriving; those bytes come out with the rest of it.
        /// </summary>
        public byte[] Process(byte[] buffer, int offset, int count)
        {
            var output = new MemoryStream(count + 16);
            var position = offset;
            var end = offset + count;

            while (position < end)
            {
                switch (_mode)
                {
                    case Mode.PassThrough:
                        output.Write(buffer, position, end - position);
                        position = end;
                        break;

                    case Mode.Body:
                        var take = (int)Math.Min(_bodyRemaining, end - position);
                        output.Write(buffer, position, take);
                        position += take;
                        _bodyRemaining -= take;

                        if (_bodyRemaining == 0)
                        {
                            _mode = Mode.Headers;
                        }
                        break;

                    case Mode.Headers:
                        position = CollectHeader(buffer, position, end, output);
                        break;
                }
            }

            return output.ToArray();
        }

        /// <summary>
        /// Adds header bytes until the blank line that ends the block, then emits it rewritten.
        /// Returns where it stopped reading.
        /// </summary>
        private int CollectHeader(byte[] buffer, int position, int end, MemoryStream output)
        {
            while (position < end)
            {
                _header.WriteByte(buffer[position]);
                position++;

                if (EndsWithBlankLine())
                {
                    EmitHeader(output);
                    return position;
                }

                if (_header.Length > MaxHeaderBytes)
                {
                    GiveUp(output, "a header block longer than " + MaxHeaderBytes + " bytes");
                    return position;
                }
            }

            return position;
        }

        private bool EndsWithBlankLine()
        {
            var length = _header.Length;

            if (length < 4) return false;

            var bytes = _header.GetBuffer();

            return bytes[length - 4] == '\r' && bytes[length - 3] == '\n'
                && bytes[length - 2] == '\r' && bytes[length - 1] == '\n';
        }

        private void EmitHeader(MemoryStream output)
        {
            var text = Latin1.GetString(_header.GetBuffer(), 0, (int)_header.Length);
            _header.SetLength(0);

            // The block ends in a blank line, so splitting leaves two empty entries at the end; they are
            // put back unchanged when it is joined again.
            var lines = text.Split(new[] { "\r\n" }, StringSplitOptions.None);

            long contentLength = 0;
            var upgrading = false;
            var chunked = false;

            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                var colon = line.IndexOf(':');

                if (colon <= 0) continue;

                var name = line.Substring(0, colon).Trim();
                var value = line.Substring(colon + 1).Trim();

                if (name.Equals("Host", StringComparison.OrdinalIgnoreCase))
                {
                    if (line.EndsWith(_fromSuffix, StringComparison.Ordinal))
                    {
                        lines[i] = line.Substring(0, line.Length - _fromSuffix.Length) + _toSuffix;
                        RewrittenRequests++;
                    }
                }
                else if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    long.TryParse(value, out contentLength);
                }
                else if (name.Equals("Upgrade", StringComparison.OrdinalIgnoreCase))
                {
                    upgrading = value.IndexOf("websocket", StringComparison.OrdinalIgnoreCase) >= 0;
                }
                else if (name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                {
                    chunked = value.IndexOf("chunked", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }

            var rewritten = Latin1.GetBytes(string.Join("\r\n", lines));
            output.Write(rewritten, 0, rewritten.Length);

            if (upgrading)
            {
                // everything after this is WebSocket frames
                _mode = Mode.PassThrough;
                PassThroughReason = "WebSocket upgrade";
            }
            else if (chunked)
            {
                _mode = Mode.PassThrough;
                PassThroughReason = "a chunked request body";
            }
            else if (contentLength > 0)
            {
                _bodyRemaining = contentLength;
                _mode = Mode.Body;
            }
            else
            {
                _mode = Mode.Headers;
            }
        }

        private void GiveUp(MemoryStream output, string reason)
        {
            output.Write(_header.GetBuffer(), 0, (int)_header.Length);
            _header.SetLength(0);
            _mode = Mode.PassThrough;
            PassThroughReason = reason;
        }
    }
}
