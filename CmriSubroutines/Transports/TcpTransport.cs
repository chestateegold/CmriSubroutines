using System;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        private int _readTimeoutMs = 2000;
        private int _writeTimeoutMs = 2000;

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

        public int ReadTimeoutMs { get => _readTimeoutMs; set => _readTimeoutMs = value; }
        public int WriteTimeoutMs { get => _writeTimeoutMs; set => _writeTimeoutMs = value; }

        public async Task Open(CancellationToken cancellationToken = default)
        {
            var addresses = Dns.GetHostAddresses(_host);
            if (addresses == null || addresses.Length == 0)
                throw new SocketException((int)SocketError.HostNotFound);

            var ip = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addresses[0];

            var connectTask = _client.ConnectAsync(ip, _port);
            using (var ctr = cancellationToken.Register(() => _client.Close()))
            {
                await connectTask.ConfigureAwait(false);
            }

            _stream = _client.GetStream();
            try
            {
                _client.ReceiveTimeout = _readTimeoutMs;
                _client.SendTimeout = _writeTimeoutMs;
            }
            catch { }
        }

        private void CloseSync()
        {
            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }
        }

        public Task Close(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CloseSync();
            return Task.CompletedTask;
        }

        public void Dispose() => CloseSync();

        private void DiscardInBufferSync()
        {
            if (_stream == null) return;
            try
            {
                while (_client.Available > 0)
                {
                    var buf = new byte[ReadBufferSize];
                    int toRead = Math.Min(_client.Available, buf.Length);
                    int read = _stream.Read(buf, 0, toRead);
                    if (read <= 0) break;
                }
            }
            catch { }
        }

        public Task DiscardInBuffer(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DiscardInBufferSync();
            return Task.CompletedTask;
        }

        private void DiscardOutBufferSync()
        {
            // nothing to do for TCP
        }

        public Task DiscardOutBuffer(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DiscardOutBufferSync();
            return Task.CompletedTask;
        }
                
        public async Task<int> ReadByte(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                await Task.FromCanceled<int>(cancellationToken).ConfigureAwait(false);

            return await ReadByteAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> ReadByteAsync(CancellationToken cancellationToken = default)
        {
            if (_stream == null) throw new InvalidOperationException("Transport not open");
            var buffer = new byte[1];
            int read = await _stream.ReadAsync(buffer, 0, 1, cancellationToken).ConfigureAwait(false);
            if (read == 0) return -1;
            return buffer[0];
        }

        public Task<int> Read(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            return ReadAsync(buffer, offset, count, cancellationToken);
        }

        public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            if (_stream == null) throw new InvalidOperationException("Transport not open");
            return _stream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public Task Write(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            return WriteAsync(buffer, offset, count, cancellationToken);
        }

        public async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            if (_stream == null) throw new InvalidOperationException("Transport not open");
            await _stream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
