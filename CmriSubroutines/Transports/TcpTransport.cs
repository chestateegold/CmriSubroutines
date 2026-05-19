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
        private readonly int _bufferSize;
        private readonly int _timeoutMs;

        public TcpTransport(string host, int port, int timeoutMs, int bufferSize)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port;
            _bufferSize = bufferSize;
            _timeoutMs = timeoutMs;
        }

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
        public Task Open(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            //TODO: check that this is necessary and if it should happen after the client is connected and not before
            DiscardInBufferSync();
            DiscardOutBufferSync();

            var addresses = Dns.GetHostAddresses(_host);
            if (addresses == null || addresses.Length == 0)
                throw new SocketException((int)SocketError.HostNotFound);

            var ip = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addresses[0];

            var connectTask = _client.ConnectAsync(ip, _port);
            using (var ctr = cancellationToken.Register(() => _client.Close()))
            {
                connectTask.GetAwaiter().GetResult();
            }

            _stream = _client.GetStream();
            try
            {
                _client.ReceiveTimeout = _timeoutMs;
                _client.SendTimeout = _timeoutMs;
            }
            catch { }

            return Task.CompletedTask;
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
                    var buf = new byte[_bufferSize];
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

            if (_stream == null) throw new InvalidOperationException("Transport not open");
            var buffer = new byte[1];
            int read = await _stream.ReadAsync(buffer, 0, 1, cancellationToken).ConfigureAwait(false);
            if (read == 0) return -1;
            return buffer[0];
        }

        public async Task Write(byte[] buffer, CancellationToken cancellationToken = default)
        {
            if (_stream == null) throw new InvalidOperationException("Transport not open");
            await _stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
