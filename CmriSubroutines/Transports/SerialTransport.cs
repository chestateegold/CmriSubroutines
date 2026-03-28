using System;
using System.IO.Ports;

namespace CmriSubroutines.Transports
{
    public class SerialTransport : ITransport
    {
        private readonly SerialPort _port;
        private int _readTimeoutMs = 2000;
        private int _writeTimeoutMs = 2000;

        public SerialTransport(int comPort, int baud100, int bufferSize)
        {
            if (comPort < 1)
                throw new ArgumentOutOfRangeException(nameof(comPort));

            _port = new SerialPort("COM" + comPort)
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
        public void Dispose() => Close();
        public void DiscardInBuffer() => _port.DiscardInBuffer();
        public void DiscardOutBuffer() => _port.DiscardOutBuffer();
        public int ReadByte() => _port.ReadByte();
        public int Read(byte[] buffer, int offset, int count) => _port.Read(buffer, offset, count);
        public void Write(byte[] buffer, int offset, int count) => _port.Write(buffer, offset, count);
    }
}
