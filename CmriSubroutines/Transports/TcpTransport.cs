using System;
using System.Net.Sockets;

namespace CmriSubroutines.Transports
{
    public class TcpTransport : ITransport
    {
        private readonly TcpClient _client = new TcpClient();
        private NetworkStream _stream;
        private readonly string _host;
        private readonly int _port;
        private int _readBufferSize = 4096;
        private int _writeBufferSize = 4096;

        public TcpTransport(string host, int port)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port;
        }

        public int ReadBufferSize { get => _readBufferSize; set => _readBufferSize = value; }
        public int WriteBufferSize { get => _writeBufferSize; set => _writeBufferSize = value; }
        public int BytesToRead
        {
            get
            {
                if (_stream == null || !_stream.DataAvailable) return 0;
                return _client.Available;
            }
        }

        public int BytesToWrite => 0; // TcpClient doesn't expose pending bytes to write easily

        public void Open()
        {
            _client.Connect(_host, _port);
            _stream = _client.GetStream();
        }

        public void Close()
        {
            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }
        }

        public void Dispose() => Close();

        public void DiscardInBuffer()
        {
            // consume available data
            if (_stream == null) return;
            while (_stream.DataAvailable)
            {
                var buf = new byte[ReadBufferSize];
                _stream.Read(buf, 0, buf.Length);
            }
        }

        public void DiscardOutBuffer()
        {
            // nothing to do for TCP
        }

        public int ReadByte()
        {
            if (_stream == null) throw new InvalidOperationException("Transport not open");
            int val = _stream.ReadByte();
            return val;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            if (_stream == null) throw new InvalidOperationException("Transport not open");
            return _stream.Read(buffer, offset, count);
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            if (_stream == null) throw new InvalidOperationException("Transport not open");
            _stream.Write(buffer, offset, count);
            _stream.Flush();
        }
    }
}
