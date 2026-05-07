using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace CmriSubroutines.Transports
{
    public class SerialTransport : ITransport
    {
        private readonly SerialPort _port;

        public SerialTransport(int comPort, BaudRate baudRate, int bufferSize)
        {
            _port = new SerialPort(NormalizeComPortName(comPort))
            {
                BaudRate = (int)baudRate,
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.Two,
                ReadBufferSize = bufferSize,
                WriteBufferSize = bufferSize
            };
        }

        public SerialTransport(string portName, BaudRate baudRate, int bufferSize)
        {
            _port = new SerialPort(NormalizePortName(portName))
            {
                BaudRate = (int)baudRate,
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.Two,
                ReadBufferSize = bufferSize,
                WriteBufferSize = bufferSize
            };
        }

        private static string NormalizePortName(string portName)
        {
            if (string.IsNullOrWhiteSpace(portName))
                throw new ArgumentNullException(nameof(portName));

            portName = portName.Trim();

            if (portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || portName.Contains("/") || portName.Contains("\\"))
                return portName;

            if (int.TryParse(portName, out int portNumber))
                return "COM" + portNumber;

            return portName;
        }

        private static string NormalizeComPortName(int comPort)
        {
            if (comPort < 1)
                throw new ArgumentOutOfRangeException(nameof(comPort));

            return "COM" + comPort;
        }
        public int BytesToRead => _port.BytesToRead;
        public int BytesToWrite => _port.BytesToWrite;
        public Task Open(CancellationToken cancellationToken = default)
        {
            DiscardInBuffer();
            DiscardOutBuffer();

            _port.Open();
            return Task.CompletedTask;
        }

        public Task Close(CancellationToken cancellationToken = default)
        {
            CloseSync();
            return Task.CompletedTask;
        }

        private void CloseSync()
        {
            if (_port.IsOpen)
                _port.Close();
        }
        public void Dispose() => CloseSync();
        public Task DiscardInBuffer(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _port.DiscardInBuffer();
            return Task.CompletedTask;
        }
        public Task DiscardOutBuffer(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _port.DiscardOutBuffer();
            return Task.CompletedTask;
        }
        public void DiscardInBufferSync() => _port.DiscardInBuffer();
        public void DiscardOutBufferSync() => _port.DiscardOutBuffer();
        private int ReadByteSync() => _port.ReadByte();
        public Task<int> ReadByte(CancellationToken cancellationToken = default)
        {
            // SerialPort doesn't provide a true async API in older frameworks; wrap the blocking call.
            return Task.Run(() => ReadByteSync(), cancellationToken);
        }
        private int ReadSync(byte[] buffer, int offset, int count) => _port.Read(buffer, offset, count);
        public Task<int> Read(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => ReadSync(buffer, offset, count), cancellationToken);
        }
        private void WriteSync(byte[] buffer, int offset, int count) => _port.Write(buffer, offset, count);
        public Task Write(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => WriteSync(buffer, offset, count), cancellationToken);
        }
    }

    /// <summary>
    /// Specifies the supported baud rates for CMRI serial communication, expressed in hundreds of bits per second.
    /// </summary>
    public enum BaudRate
    {
        B9600 = 9600,
        B19200 = 19200,
        B28800 = 28800,
        B57600 = 57600,
        B115200 = 115200
    }
}
