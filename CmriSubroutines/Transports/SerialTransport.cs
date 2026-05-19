using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CmriSubroutines.Transports
{
    public class SerialTransport : ITransport
    {
        private readonly SerialPort _port;
        private readonly ILogger _logger;

        public SerialTransport(int comPort, BaudRate baudRate, int bufferSize)
            : this(comPort, baudRate, bufferSize, NullLogger.Instance)
        {
        }

        public SerialTransport(int comPort, BaudRate baudRate, int bufferSize, ILogger logger)
        {
            _logger = logger ?? NullLogger.Instance;
            _port = new SerialPort(NormalizeComPortName(comPort))
            {
                BaudRate = (int)baudRate,
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.Two,
                ReadBufferSize = bufferSize,
                WriteBufferSize = bufferSize
            };

            _logger.LogDebug("Serial transport configured for COM{ComPort} at {BaudRate} baud.", comPort, baudRate);
        }

        public SerialTransport(string portName, BaudRate baudRate, int bufferSize)
            : this(portName, baudRate, bufferSize, NullLogger.Instance)
        {
        }

        public SerialTransport(string portName, BaudRate baudRate, int bufferSize, ILogger logger)
        {
            _logger = logger ?? NullLogger.Instance;
            _port = new SerialPort(NormalizePortName(portName))
            {
                BaudRate = (int)baudRate,
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.Two,
                ReadBufferSize = bufferSize,
                WriteBufferSize = bufferSize
            };

            _logger.LogDebug("Serial transport configured for port {PortName} at {BaudRate} baud.", portName, baudRate);
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
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Opening serial transport on {PortName}.", _port.PortName);
            _port.Open();

            DiscardInBufferSync();
            DiscardOutBufferSync();

            _logger.LogInformation("Serial transport on {PortName} opened.", _port.PortName);

            return Task.CompletedTask;
        }

        public Task Close(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Closing serial transport on {PortName}.", _port.PortName);
            CloseSync();
            _logger.LogInformation("Serial transport on {PortName} closed.", _port.PortName);

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
            _logger.LogTrace("Discarding serial input buffer on {PortName}.", _port.PortName);
            DiscardInBufferSync();

            return Task.CompletedTask;
        }

        public Task DiscardOutBuffer(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogTrace("Discarding serial output buffer on {PortName}.", _port.PortName);
            DiscardOutBufferSync();

            return Task.CompletedTask;
        }

        private void DiscardInBufferSync() => _port.DiscardInBuffer();

        private void DiscardOutBufferSync() => _port.DiscardOutBuffer();

        private int ReadByteSync() => _port.ReadByte();

        public Task<int> ReadByte(CancellationToken cancellationToken = default)
        {
            // SerialPort doesn't provide a true async API in older frameworks; wrap the blocking call.
            _logger.LogTrace("Reading a byte from serial transport on {PortName}.", _port.PortName);
            return Task.Run(() => ReadByteSync(), cancellationToken);
        }

        private void WriteSync(byte[] buffer) => _port.Write(buffer, 0, buffer.Length);

        public Task Write(byte[] buffer, CancellationToken cancellationToken = default)
        {
            _logger.LogTrace("Writing {Count} bytes to serial transport on {PortName}.", buffer == null ? 0 : buffer.Length, _port.PortName);
            return Task.Run(() => WriteSync(buffer), cancellationToken);
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
