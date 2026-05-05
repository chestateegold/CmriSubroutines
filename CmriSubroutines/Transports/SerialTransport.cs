using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace CmriSubroutines.Transports
{
    public class SerialTransport : ITransport
    {
        private readonly SerialPort _port;
        private int _readTimeoutMs = 2000;
        private int _writeTimeoutMs = 2000;

        private static string NormalizePortName(string portName)
        {
            if (string.IsNullOrWhiteSpace(portName))
                throw new ArgumentNullException(nameof(portName));

            portName = portName.Trim();

            if (portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || portName.Contains("/") || portName.Contains("\\"))
                return portName;

            int portNumber;
            if (int.TryParse(portName, out portNumber))
                return "COM" + portNumber;

            return portName;
        }

        private static string NormalizeComPortName(int comPort)
        {
            if (comPort < 1)
                throw new ArgumentOutOfRangeException(nameof(comPort));

            return "COM" + comPort;
        }

        public SerialTransport(int comPort, int baud100, int bufferSize)
            : this(NormalizeComPortName(comPort), baud100, bufferSize)
        {
        }

        public SerialTransport(string portName, int baud100, int bufferSize)
        {
            //TODO: validate baud rate
            _port = new SerialPort(NormalizePortName(portName))
            {
                BaudRate = baud100 * 100,
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.Two,
                ReadBufferSize = bufferSize,
                WriteBufferSize = bufferSize
            };
        }

        public int ReadBufferSize { get => _port.ReadBufferSize; set => _port.ReadBufferSize = value; }
        public int WriteBufferSize { get => _port.WriteBufferSize; set => _port.WriteBufferSize = value; }
        public int BytesToRead => _port.BytesToRead;
        public int BytesToWrite => _port.BytesToWrite;
        public int ReadTimeoutMs { get => _readTimeoutMs; set { _readTimeoutMs = value; _port.ReadTimeout = value; } }
        public int WriteTimeoutMs { get => _writeTimeoutMs; set { _writeTimeoutMs = value; _port.WriteTimeout = value; } }

        public void Open() => _port.Open();
        public void Close() { if (_port.IsOpen) _port.Close(); }
        public Task OpenAsync(CancellationToken cancellationToken = default)
        {
            Open();
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            Close();
            return Task.CompletedTask;
        }
        public void Dispose() => Close();
        public void DiscardInBuffer() => _port.DiscardInBuffer();
        public void DiscardOutBuffer() => _port.DiscardOutBuffer();
        public int ReadByte() => _port.ReadByte();
        public Task<int> ReadByteAsync(CancellationToken cancellationToken = default)
        {
            // SerialPort doesn't provide a true async API in older frameworks; wrap the blocking call.
            return Task.Run(() => ReadByte(), cancellationToken);
        }
        public int Read(byte[] buffer, int offset, int count) => _port.Read(buffer, offset, count);
        public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Read(buffer, offset, count), cancellationToken);
        }
        public void Write(byte[] buffer, int offset, int count) => _port.Write(buffer, offset, count);
        public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Write(buffer, offset, count), cancellationToken);
        }
    }
}
