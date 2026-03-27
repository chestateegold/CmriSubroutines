using System;
using System.Net;
using System.Net.Sockets;
using System.Linq;

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
                if (_stream == null) return 0;
                try { return _client.Available; }
                catch { return 0; }
            }
        }

        public int BytesToWrite => 0; // TcpClient doesn't expose pending bytes to write easily

        public void Open()
        {
            var addresses = Dns.GetHostAddresses(_host);
            if (addresses == null || addresses.Length == 0)
                throw new SocketException((int)SocketError.HostNotFound);

            var ip = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addresses[0];

            var connectTask = _client.ConnectAsync(ip, _port);
            if (!connectTask.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException($"Connect to {_host}:{_port} timed out");

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
            if (_stream == null) return;
            try
            {
                while (_client.Available > 0)
                {
                    var buf = new byte[ReadBufferSize];
                    int toRead = Math.Min(_client.Available, buf.Length);
                    _stream.Read(buf, 0, toRead);
                }
            }
            catch { }
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
