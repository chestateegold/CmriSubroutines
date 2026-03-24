using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.IO.Ports;

namespace CmriSubroutines
{
    public class Subroutines
    {
        private ITransport _transport;
        private readonly int _maxTries;
        private readonly int _delay;
        private readonly int _maxBuf;

        /// <summary>
        /// Initializes the Serial Port Communications Object with default 1152 baud rate, 1500 max tries, 0 delay and 64 maxbuf
        /// </summary>
        /// <param name="ComPort"></param>
        public Subroutines(int ComPort) : this(ComPort, 1152, 3000, 0, 64)
        {
        }

        /// <summary>
        /// Initializes the Serial Port Communications Object with explicit values
        /// </summary>
        /// <param name="ComPort"></param>
        /// <param name="Baud100"></param>
        /// <param name="MaxTries"></param>
        /// <param name="Delay"></param>
        /// <param name="MaxBuf"></param>
        public Subroutines(int ComPort, int Baud100, int MaxTries, int Delay, int MaxBuf)
            : this(new SerialTransport(ComPort, Baud100, MaxBuf), MaxTries, Delay, MaxBuf)
        {
        }

        /// <summary>
        /// Core constructor that accepts an ITransport. This is used by the public overloads
        /// to preserve existing API while allowing transport injection.
        /// </summary>
        /// <param name="transport"></param>
        /// <param name="MaxTries"></param>
        /// <param name="Delay"></param>
        /// <param name="MaxBuf"></param>
        public Subroutines(ITransport transport, int MaxTries, int Delay, int MaxBuf)
        {
            if (transport == null)
                throw new ArgumentNullException(nameof(transport));

            if (MaxTries <= 0)
                throw new ArgumentOutOfRangeException("MaxTries", "MaxTries must be a positive");

            if (Delay < 0)
                throw new ArgumentOutOfRangeException("Delay", "Delay can not be less than zero");

            if (MaxBuf <= 0)
                throw new ArgumentOutOfRangeException("MaxBuf", "MaxBuf must be a positive");

            _maxTries = MaxTries;
            _delay = Delay;
            _maxBuf = MaxBuf;

            _transport = transport;
            _transport.ReadBufferSize = _maxBuf;
            _transport.WriteBufferSize = _maxBuf;
            _transport.Open();
            _transport.DiscardInBuffer();
            _transport.DiscardOutBuffer();
        }

        /// <summary>
        /// Initializes specified node.
        /// </summary>
        /// <param name="UA">Node Address</param>
        /// <param name="NodeType"></param>
        public void Init(int UA, NodeType NodeType)
        {
            // ensure this isn't a maxi. we are required to have a CT array with maxi
            if (NodeType == NodeType.MAXI24 || NodeType == NodeType.MAXI32)
            {
                throw new ArgumentNullException("CT", "CT Parameter is required for MAXI Nodes");
            }

            Init(UA, NodeType, new byte[] { 0, 0, 0, 0, 0, 0 });
        }

        /// <summary>
        /// Initializes specified node with CT parameter.
        /// </summary>
        /// <param name="UA">Node Address</param>
        /// <param name="NodeType"></param>
        /// <param name="CT">Card type array for MAXI nodes and SMINI 2 lead signals</param>
        public void Init(int UA, NodeType NodeType, byte[] CT)
        {
            // validation
            if (UA > 127)
                throw new ArgumentOutOfRangeException("UA", "Valid UA range is 0-127");

            if ((NodeType == NodeType.MAXI24 || NodeType == NodeType.MAXI32) && (CT == null || CT.Length == 0))
                throw new ArgumentNullException("CT", "CT Parameter is required for MAXI Nodes");

            if (NodeType == NodeType.SMINI && CT.Length != 6)
                throw new ArgumentException("CT", "CT array requires 6 elements for SMINI dual lead signals");

            // create first part of buffer to be sent to railroad
            byte[] outputBuffer = new byte[3];

            switch (NodeType) // set node definition parameter
            {
                case NodeType.SMINI:
                    outputBuffer[0] = (byte)'M';
                    break;
                case NodeType.MAXI24:
                    outputBuffer[0] = (byte)'N';
                    break;
                case NodeType.MAXI32:
                    outputBuffer[0] = (byte)'X';
                    break;
                case NodeType.CPNODE:
                    outputBuffer[0] = (byte)'C';
                    break;
            }
            outputBuffer[1] = (byte)(_delay / 256);
            outputBuffer[2] = (byte)(_delay - (outputBuffer[1] * 256));

            if (NodeType == NodeType.MAXI24 || NodeType == NodeType.MAXI32)
                outputBuffer = outputBuffer.Concat(GetMaxiInitBytes(CT)).ToArray();
            else if (NodeType == NodeType.SMINI)
                outputBuffer = outputBuffer.Concat(GetSminiInitBytes(CT)).ToArray();
            else if (NodeType == NodeType.CPNODE)
                outputBuffer = outputBuffer.Concat(new byte[1] { 0 }).ToArray();

            TransmitPackage(UA, 'I', outputBuffer);
        }

        /// <summary>
        /// Generates and validates the parts of the output buffer that concern the CT array for the SMINI
        /// </summary>
        /// <param name="CT"></param>
        /// <returns></returns>
        private byte[] GetSminiInitBytes(byte[] CT)
        {
            // validate and count the CT array
            int twoLeadSignalCount = 0; // aka NS

            // loop through each card in the CT array to count and validate the locations of 2 lead signals
            for (int i = 0; i < CT.Length; i++)
            {
                if (CT[i] == 0)
                    continue;

                /* bitwise function to check if an odd number of bites are consecutively high.
                 * an odd number of high bits in a row is invalid. */
                int successiveHighBits = 0;
                for (int j = 0; j <= 8; j++) // goes to 8 so we can guarantee a 0 so we don't miss the last digit
                {
                    // checks the bit at index j to see if it is set
                    if ((CT[i] & 1 << j) != 0)
                    {
                        successiveHighBits++;
                    }
                    else if (successiveHighBits % 2 == 0) // even high bit groups indicates valid 2 lead signal configuration
                    {
                        twoLeadSignalCount += successiveHighBits / 2; // one signal for every 2 high bits in a row
                        successiveHighBits = 0;
                    }
                    else
                    {
                        throw new ArgumentException("CT",
                            $"CT array value at index: ${i} with value: ${CT[i]} " +
                            $"contains invalid dual lead signal positions");
                    }
                }
            }

            // build the ct portion of output buffer
            int outputBufferLength = twoLeadSignalCount > 0 ? 1 + CT.Length : 1;
            byte[] ctOutputBuffer = new byte[outputBufferLength];

            // number of 2 lead signals
            ctOutputBuffer[0] = (byte)twoLeadSignalCount;

            // copy ct array to output buffer
            if (twoLeadSignalCount > 0)
                CT.CopyTo(ctOutputBuffer, 1);

            return ctOutputBuffer;
        }

        /// <summary>
        /// Generates and validates the parts of the output buffer that concern the CT array for the MAXI node
        /// </summary>
        /// <param name="CT"></param>
        /// <returns></returns>
        private byte[] GetMaxiInitBytes(byte[] CT)
        {
            // loop through each card in the CT array to count and validate the locations of IO cards
            for (int i = 0; i < CT.Length; i++)
            {
                /* bitwise function to ensure slots are not set to both input and output */
                for (int j = 0; j < 8; j += 2) // goes to 8 so we can guarantee a 0 so we don't miss the last digit
                {
                    // checks to see if the bit is set for either an input or output
                    if ((CT[i] & 1 << j) != 0 && (CT[i] & 1 << j + 1) != 0)
                    {
                        throw new ArgumentException("CT",
                            $"CT array value at index: ${i} with value: ${CT[i]} " +
                            $"contains invalid input and output board positions. Slot can not be both input and output");
                    }
                }
            }

            // build the ct portion of output buffer
            byte[] ctOutputBuffer = new byte[1 + CT.Length];

            // number of cards/ 4, rounded up
            int NS = CT.Length;
            ctOutputBuffer[0] = (byte)NS;

            // copy ct array to output buffer
            CT.CopyTo(ctOutputBuffer, 1);

            return ctOutputBuffer;
        }

        /// <summary>
        /// Gets all inputs from smini node
        /// </summary>
        /// <param name="UA"></param>
        /// <returns></returns>
        public byte[] Inputs(int UA)
        {
            byte[] inputs = new byte[3];
            byte iInByte;

            // Transmit poll loop
            bool poll = true;
            while (poll)
            {
                // clears input buffer
                _transport.DiscardInBuffer();

                // Polls node
                TransmitPackage(UA, 'P', inputs);

                // loop to get start of transmission (stx)
                bool stx = false;
                while (!stx)
                {
                    iInByte = ReceiveByte(UA);

                    if (iInByte != 2) // this message is not the start of the transmission, retry
                        continue;
                    else
                        poll = false;

                    // now checking for the UA
                    iInByte = ReceiveByte(UA);
                    if (iInByte - 65 != UA)
                    {
                        Console.WriteLine("ERROR; Received bad UA = " + iInByte);
                        break; // this has returned the wrong UA, repol.
                    }

                    // check that the message is an 'R' message
                    iInByte = ReceiveByte(UA);
                    if (iInByte != 82)
                    {
                        Console.WriteLine("Error received not = R for UA = " + UA);
                        continue;
                    }

                    stx = true;
                }

                if (stx)
                {
                    // begin looping through inputs. Hardcoded for smini
                    for (int i = 0; i < 3; i++)
                    {
                        iInByte = ReceiveByte(UA);

                        if (iInByte == 2)
                            throw new InvalidOperationException("ERROR: No DLE ahead of 2 for UA = " + UA);
                        else if (iInByte == 3)
                            throw new InvalidOperationException("ERROR: No DLE ahead of 3 for UA = " + UA);
                        else if (iInByte == 16) // this is the escape character
                            iInByte = ReceiveByte(UA);

                        inputs[i] = iInByte;
                    }

                    // check for ETX
                    iInByte = ReceiveByte(UA);
                    if (iInByte != 3)
                        Console.WriteLine("ERROR: ETX NOT PROPERLY RECEIVED FOR UA = " + UA);
                }
            }

            return inputs;
        }

        /// <summary>
        /// Sends outputs to a specified node
        /// </summary>        
        /// <param name="UA"></param>
        /// <param name="OutputBuffer"></param>
        public void Outputs(int UA, byte[] OutputBuffer)
        {
            // should be some validation here
            _transport.DiscardOutBuffer();
            TransmitPackage(UA, 'T', OutputBuffer); // 84 is message type "T"
        }

        /// <summary>
        /// Transmits outputs to a specified node
        /// </summary>
        /// <param name="CommObj">Comm object used to communivate with nodes</param>
        /// <param name="UA">USIC Address of node</param>
        /// <param name="MessageType"></param>
        /// <param name="OutputBuffer">Data to be output</param>
        private void TransmitPackage(int UA, int MessageType, byte[] OutputBuffer)
        {
            // buffer that heads to node
            byte[] bTransmitBuffer = new byte[80];

            // pointer for transmit buffer

            bTransmitBuffer[0] = 255;
            bTransmitBuffer[1] = 255;
            bTransmitBuffer[2] = 2;
            bTransmitBuffer[3] = (byte)(UA + 65);
            bTransmitBuffer[4] = (byte)MessageType;

            int iXmitPointer = 5; // transmit buffer begins at 6th byte, first 5 are header info

            _transport.DiscardOutBuffer();

            /* Write data from output buffer to transmit buffer. */
            if (MessageType != 80) // 80 is a poll request, head to end message
            {
                foreach (byte b in OutputBuffer)
                {
                    if (b == 2 || b == 3 || b == 16) // escapes command bytes
                    {
                        bTransmitBuffer[iXmitPointer] = 16;
                        iXmitPointer++;
                    }

                    bTransmitBuffer[iXmitPointer] = b;
                    iXmitPointer++;
                }
            }

            /* ENDMSG */
            bTransmitBuffer[iXmitPointer] = 3;
            iXmitPointer++;

            /* Transmit message to railroad */
            _transport.Write(bTransmitBuffer, 0, iXmitPointer);

            while (_transport.BytesToWrite > 0) // allows buffer to empty if it is taking long         
                Thread.Sleep(10);
        }

        /// <summary>
        /// Loops until an input byte is detected in the buffer or the number of maxTries is reached
        /// </summary>
        /// <param name="UA">USIC Address of node</param>
        /// <returns></returns>
        private byte ReceiveByte(int UA)
        {
            int tries = 0;
            do
            {
                if (_transport.BytesToRead > _maxBuf)
                    throw new OverflowException("Node " + UA + " bytes to read is over MaxBuf value of " + _maxBuf);

                if (_transport.BytesToRead != 0)
                    break;

                tries++;
            } while (tries < _maxTries);

            if (tries == _maxTries)
                throw new TimeoutException("INPUT TRIES EXCEEDED " + _maxTries + " NODE = " + UA + " ABORTING INPUT");

            return (byte)_transport.ReadByte();
        }
    }

    public enum NodeType
    {
        SMINI,
        MAXI24,
        MAXI32,
        CPNODE
    }
}

namespace CmriSubroutines
{
    /// <summary>
    /// Transport abstraction to allow swapping underlying communication mechanism.
    /// </summary>
    public interface ITransport : IDisposable
    {
        void Open();
        void Close();
        int ReadByte();
        int Read(byte[] buffer, int offset, int count);
        void Write(byte[] buffer, int offset, int count);
        void DiscardInBuffer();
        void DiscardOutBuffer();
        int BytesToRead { get; }
        int BytesToWrite { get; }
        int ReadBufferSize { get; set; }
        int WriteBufferSize { get; set; }
    }

    /// <summary>
    /// Serial port transport that wraps System.IO.Ports.SerialPort
    /// </summary>
    public class SerialTransport : ITransport
    {
        private readonly SerialPort _port;

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

        public void Open() => _port.Open();
        public void Close() { if (_port.IsOpen) _port.Close(); }
        public void Dispose() => Close();
        public void DiscardInBuffer() => _port.DiscardInBuffer();
        public void DiscardOutBuffer() => _port.DiscardOutBuffer();
        public int ReadByte() => _port.ReadByte();
        public int Read(byte[] buffer, int offset, int count) => _port.Read(buffer, offset, count);
        public void Write(byte[] buffer, int offset, int count) => _port.Write(buffer, offset, count);
    }

    /// <summary>
    /// TCP transport using TcpClient. Supports basic raw TCP (ser2net raw mode).
    /// </summary>
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

    /// <summary>
    /// Simple factory to create transports based on a config object.
    /// </summary>
    public static class TransportFactory
    {
        public static ITransport CreateSerial(int comPort, int baud100, int bufferSize)
        {
            return new SerialTransport(comPort, baud100, bufferSize);
        }

        public static ITransport CreateTcp(string host, int port)
        {
            return new TcpTransport(host, port);
        }
    }
}
