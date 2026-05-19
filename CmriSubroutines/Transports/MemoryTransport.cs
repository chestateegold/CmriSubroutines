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

        private void OpenSync()
        {
            _isOpen = true;
        }

        public Task Open(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenSync();
            return Task.CompletedTask;
        }

        private void CloseSync()
        {
            _isOpen = false;
        }

        public Task Close(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CloseSync();
            return Task.CompletedTask;
        }

        public void Dispose() => CloseSync();

        private int ReadByteSync()
        {
            EnsureOpen();

            lock (_syncRoot)
            {
                if (_readBuffer.Count == 0)
                    throw new TimeoutException("No bytes are available in the memory transport read buffer.");

                return _readBuffer.Dequeue();
            }
        }

        public Task<int> ReadByte(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ReadByteSync());
        }

        private void WriteSync(byte[] buffer)
        {
            EnsureOpen();

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            lock (_syncRoot)
            {
                _writeHistory.Add(buffer);
            }
        }

        public Task Write(byte[] buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteSync(buffer);
            return Task.CompletedTask;
        }

        private void DiscardInBufferSync()
        {
            lock (_syncRoot)
            {
                //TODO: for now commenting out to see if tests as written work correctly
                // for more complex tests, we may need to have a smarter way to time enqueueing of data
               // _readBuffer.Clear();
            }
        }

        public Task DiscardInBuffer(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DiscardInBufferSync();
            return Task.CompletedTask;
        }

        private void DiscardOutBufferSync()
        {
        }

        public Task DiscardOutBuffer(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DiscardOutBufferSync();
            return Task.CompletedTask;
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
