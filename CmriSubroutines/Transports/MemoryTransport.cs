using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CmriSubroutines.Transports
{
    public class MemoryTransport : ITransport
    {
        private readonly Queue<byte> _readBuffer = new Queue<byte>();
        private readonly List<byte[]> _writeHistory = new List<byte[]>();
        private readonly object _syncRoot = new object();
        private bool _isOpen;
        private int _readBufferSize = 4096;
        private int _writeBufferSize = 4096;
        private int _readTimeoutMs = 2000;
        private int _writeTimeoutMs = 2000;

        public MemoryTransport()
        {
        }

        public MemoryTransport(IEnumerable<byte> initialReadBuffer)
        {
            if (initialReadBuffer == null)
                return;
            //TODO: rather than enqueueing read data here, we could store locally and actually only enqueue after discardinbuffer is called
            // if we had a list of byte arrays, we could sequence in that fashion
            // the flow would be discard -> enqueue new data -> discard -> enqueue next bit of data
            // for now just commenting out DiscardInBuffer
            EnqueueRead(initialReadBuffer);
        }

        public int ReadBufferSize { get => _readBufferSize; set => _readBufferSize = value; }
        public int WriteBufferSize { get => _writeBufferSize; set => _writeBufferSize = value; }
        public int ReadTimeoutMs { get => _readTimeoutMs; set => _readTimeoutMs = value; }
        public int WriteTimeoutMs { get => _writeTimeoutMs; set => _writeTimeoutMs = value; }

        public int BytesToRead
        {
            get
            {
                lock (_syncRoot)
                {
                    return _readBuffer.Count;
                }
            }
        }

        public int BytesToWrite => 0;

        public void Open()
        {
            _isOpen = true;
        }

        public Task OpenAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Open();
            return Task.CompletedTask;
        }

        public void Close()
        {
            _isOpen = false;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Close();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Close();
        }

        public int ReadByte()
        {
            EnsureOpen();

            lock (_syncRoot)
            {
                if (_readBuffer.Count == 0)
                    throw new TimeoutException("No bytes are available in the memory transport read buffer.");

                return _readBuffer.Dequeue();
            }
        }

        public Task<int> ReadByteAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ReadByte());
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            EnsureOpen();

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            lock (_syncRoot)
            {
                int read = 0;
                while (read < count && _readBuffer.Count > 0)
                {
                    buffer[offset + read] = _readBuffer.Dequeue();
                    read++;
                }

                return read;
            }
        }

        public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Read(buffer, offset, count));
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            EnsureOpen();

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            lock (_syncRoot)
            {
                var writtenBytes = new byte[count];
                Array.Copy(buffer, offset, writtenBytes, 0, count);
                _writeHistory.Add(writtenBytes);
            }
        }

        public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }

        public void DiscardInBuffer()
        {
            lock (_syncRoot)
            {
                //TODO: for now commenting out to see if tests as written work correctly
                // for more complex tests, we may need to have a smarter way to time enqueueing of data
               // _readBuffer.Clear();
            }
        }

        public void DiscardOutBuffer()
        {
        }

        public void EnqueueRead(params byte[] data)
        {
            EnqueueRead((IEnumerable<byte>)data);
        }

        public void EnqueueRead(IEnumerable<byte> data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            lock (_syncRoot)
            {
                foreach (var value in data)
                    _readBuffer.Enqueue(value);
            }
        }

        public IReadOnlyList<byte[]> GetWrittenWrites()
        {
            lock (_syncRoot)
            {
                return _writeHistory.Select(write => write.ToArray()).ToArray();
            }
        }

        public byte[] GetWrittenWrite(int writeIndex)
        {
            lock (_syncRoot)
            {
                if (writeIndex < 0 || writeIndex >= _writeHistory.Count)
                    throw new ArgumentOutOfRangeException(nameof(writeIndex));

                return _writeHistory[writeIndex].ToArray();
            }
        }

        private void EnsureOpen()
        {
            if (!_isOpen)
                throw new InvalidOperationException("Transport not open");
        }
    }
}
