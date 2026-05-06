using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using CmriSubroutines.Transports;

namespace CmriSubroutines
{
    /// <summary>
    /// Provides methods for initializing, configuring, and communicating with CMRI nodes over various transport layers,
    /// including serial, TCP, and in-memory transports. Supports asynchronous operations for reading inputs, writing
    /// outputs, and managing node configurations.
    /// </summary>
    /// <remarks>The Subroutines class abstracts communication with nodes using different transport
    /// mechanisms, allowing for flexible deployment in hardware, network, or testing environments. It manages node
    /// configuration state internally and provides asynchronous methods for protocol-compliant data exchange.
    /// Exceptions are thrown for protocol violations, invalid arguments, or transport errors to ensure robust error
    /// handling. Thread safety is not guaranteed; callers should ensure appropriate synchronization if accessing
    /// instances from multiple threads.</remarks>
    public class Subroutines
    {
        private Dictionary<int, NodeConfiguration> _nodeConfigurations = new Dictionary<int, NodeConfiguration>();
        private ITransport _transport;
        private readonly int _timeoutMs;
        private readonly int _delay;
        private readonly int _maxBuf;

        /// <summary>
        /// Creates a new Subroutines instance configured to communicate over a serial port with the specified
        /// parameters. Use this factory method for use with typical windows CMRI implementations.
        /// </summary>
        /// <param name="comPort">The number of the COM port to use for serial communication. Must correspond to a valid and available serial
        /// port on the system.</param>
        /// <param name="baud100">The baud rate for the serial connection, specified in units of 100 bits per second. The default is 96
        /// (representing 9600 bps).</param>
        /// <param name="timeoutMs">The maximum time, in milliseconds, to wait for serial operations before timing out. The default is 3000
        /// milliseconds.</param>
        /// <param name="delay">The delay, in milliseconds, to wait between operations. The default is 0 milliseconds.</param>
        /// <param name="maxBuf">The maximum buffer size, in bytes, for serial communication. The default is 64 bytes.</param>
        /// <returns>A Subroutines instance configured to use the specified serial port settings.</returns>
        public static Subroutines CreateSerial(int comPort, int baud100 = 96, int timeoutMs = 3000, int delay = 0, int maxBuf = 64)
        {
            return new Subroutines(new SerialTransport(comPort, baud100, maxBuf), timeoutMs, delay, maxBuf);
        }

        /// <summary>
        /// Creates a new Subroutines instance configured to communicate over a serial port with the specified settings. Use this factory for 
        /// </summary>
        /// <param name="portName">The name of the serial port to use for communication. For example, "COM1" or "/dev/ttyS0".</param>
        /// <param name="baud100">The baud rate for the serial connection, specified in units of 100 bits per second. The default is 96
        /// (corresponding to 9600 bps).</param>
        /// <param name="timeoutMs">The maximum time, in milliseconds, to wait for serial operations before timing out. The default is 3000.</param>
        /// <param name="delay">The delay, in milliseconds, to wait between operations. The default is 0.</param>
        /// <param name="maxBuf">The maximum buffer size, in bytes, for serial communication. The default is 64.</param>
        /// <returns>A Subroutines instance configured to use the specified serial port and communication parameters.</returns>
        public static Subroutines CreateSerial(string portName, int baud100 = 96, int timeoutMs = 3000, int delay = 0, int maxBuf = 64)
        {
            return new Subroutines(new SerialTransport(portName, baud100, maxBuf), timeoutMs, delay, maxBuf);
        }

        /// <summary>
        /// Creates a new Subroutines instance that communicates over a TCP connection to the specified host and port.
        /// </summary>
        /// <param name="host">The DNS name or IP address of the remote host to connect to. Cannot be null or empty.</param>
        /// <param name="port">The TCP port number on the remote host to connect to. Must be between 0 and 65535.</param>
        /// <param name="timeoutMs">The maximum time, in milliseconds, to wait for each operation before timing out. Must be greater than zero.
        /// The default is 3000 milliseconds.</param>
        /// <param name="delay">The delay, in milliseconds, to wait between operations. Must be zero or greater. The default is 0.</param>
        /// <param name="maxBuf">The maximum buffer size, in bytes, used for data transfers. Must be greater than zero. The default is 64
        /// bytes.</param>
        /// <returns>A Subroutines instance configured to use a TCP transport with the specified connection and settings.</returns>
        public static Subroutines CreateTcp(string host, int port, int timeoutMs = 3000, int delay = 0, int maxBuf = 64)
        {
            return new Subroutines(new TcpTransport(host, port), timeoutMs, delay, maxBuf);
        }

        /// <summary>
        /// Creates a new Subroutines instance that uses an in-memory transport, optionally initialized with a specified
        /// read buffer. Does not communicate with CMRI hardware
        /// </summary>
        /// <remarks>This method is useful for testing or scenarios where a memory-based transport is
        /// required instead of actual hardware or network communication.</remarks>
        /// <param name="initialReadBuffer">An optional sequence of bytes to initialize the memory transport's read buffer. If null, the buffer is
        /// empty.</param>
        /// <param name="timeoutMs">The maximum time, in milliseconds, to wait for an operation before timing out. Must be non-negative.</param>
        /// <param name="delay">The delay, in milliseconds, to wait between operations. Must be non-negative.</param>
        /// <param name="maxBuf">The maximum buffer size, in bytes, for the memory transport. Must be positive.</param>
        /// <returns>A Subroutines instance configured to use an in-memory transport with the specified parameters.</returns>
        public static Subroutines CreateMemory(IEnumerable<byte> initialReadBuffer = null, int timeoutMs = 3000, int delay = 0, int maxBuf = 64)
        {
            return new Subroutines(initialReadBuffer == null ? new MemoryTransport() : new MemoryTransport(initialReadBuffer), timeoutMs, delay, maxBuf);
        }

        /// <summary>
        /// Initializes a new instance of the Subroutines class with the specified transport, timeout, delay, and buffer
        /// size settings.
        /// </summary>
        /// <remarks>The transport's read and write buffer sizes are set to the specified maximum buffer
        /// size, and the transport is opened and cleared upon initialization.</remarks>
        /// <param name="Transport">The transport interface used for communication. Cannot be null.</param>
        /// <param name="TimeoutMs">The maximum time, in milliseconds, to wait for transport operations. Must be positive.</param>
        /// <param name="Delay">The delay, in milliseconds, to apply between operations. Must be zero or greater.</param>
        /// <param name="MaxBuf">The maximum buffer size, in bytes, for both read and write operations. Must be positive.</param>
        /// <exception cref="ArgumentNullException">Thrown if Transport is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if TimeoutMs is less than or equal to zero, Delay is less than zero, or MaxBuf is less than or equal
        /// to zero.</exception>
        public Subroutines(ITransport Transport, int TimeoutMs, int Delay, int MaxBuf)
        {
            if (Transport == null)
                throw new ArgumentNullException(nameof(Transport));

            if (TimeoutMs <= 0)
                throw new ArgumentOutOfRangeException("TimeoutMs", "TimeoutMs must be positive");

            if (Delay < 0)
                throw new ArgumentOutOfRangeException("Delay", "Delay can not be less than zero");

            if (MaxBuf <= 0)
                throw new ArgumentOutOfRangeException("MaxBuf", "MaxBuf must be a positive");

            _timeoutMs = TimeoutMs;
            _delay = Delay;
            _maxBuf = MaxBuf;

            _transport = Transport;
            _transport.ReadBufferSize = _maxBuf;
            _transport.WriteBufferSize = _maxBuf;
            _transport.Open();
            _transport.DiscardInBuffer();
            _transport.DiscardOutBuffer();
        }

        /// <summary>
        /// Asynchronously reads a single byte from the transport for the specified unit address, waiting up to the
        /// configured timeout if necessary.
        /// </summary>
        /// <remarks>If no data is immediately available, the method waits until a byte is received or the
        /// timeout elapses. If the operation times out, a TimeoutException is thrown. If the operation is canceled, an
        /// OperationCanceledException is thrown.</remarks>
        /// <param name="UA">The unit address associated with the read operation. Used for error reporting if a timeout occurs.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the read operation.</param>
        /// <returns>A task that represents the asynchronous read operation. The result contains the byte read from the
        /// transport.</returns>
        /// <exception cref="IOException">Thrown if an error occurs while reading from the transport.</exception>
        private async Task<byte> ReceiveByte(int UA, CancellationToken cancellationToken = default)
        {
            try
            {
                //TODO: could probably do all this nicely with a dowhile loop
                if (_transport.BytesToRead > 0)
                {
                    byte iInByte = (byte)await _transport.ReadByteAsync(cancellationToken).ConfigureAwait(false);
                    return iInByte;
                }

                int timeoutMs = _timeoutMs;
                int elapsed = 0;
                int pollInterval = 10; // ms

                while (elapsed < timeoutMs)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (_transport.BytesToRead > 0)
                    {
                        byte iInByte = (byte)await _transport.ReadByteAsync(cancellationToken).ConfigureAwait(false);
                        return iInByte;
                    }
                        

                    await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
                    elapsed += pollInterval;
                }

                throw new TimeoutException($"INPUT TIMEOUT EXCEEDED {_timeoutMs} NODE = {UA} ABORTING INPUT");
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new IOException("Error reading byte from transport", ex);
            }
        }

        /// <summary>
        /// Transmits a data package to the transport layer using the specified addressing and message type.
        /// </summary>
        /// <remarks>The method encodes the output buffer according to the specified message type and
        /// ensures that the data is fully written to the transport before completing. If the cancellation token is
        /// triggered, the operation is canceled before completion.</remarks>
        /// <param name="UA">The unit address to which the package is sent. Must be a non-negative integer.</param>
        /// <param name="MessageType">The type of message to transmit. Determines how the output buffer is processed and encoded.</param>
        /// <param name="OutputBuffer">The data payload to include in the transmitted package. Cannot be null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous transmit operation.</param>
        /// <returns>A task that represents the asynchronous transmit operation.</returns>
        private async Task TransmitPackage(int UA, int MessageType, byte[] OutputBuffer, CancellationToken cancellationToken = default)
        {
            byte[] bTransmitBuffer = new byte[80];
            bTransmitBuffer[0] = 255;
            bTransmitBuffer[1] = 255;
            bTransmitBuffer[2] = 2;
            bTransmitBuffer[3] = (byte)(UA + 65);
            bTransmitBuffer[4] = (byte)MessageType;

            int iXmitPointer = 5;

            _transport.DiscardOutBuffer();

            if (MessageType != 80)
            {
                foreach (byte b in OutputBuffer)
                {
                    if (b == 2 || b == 3 || b == 16)
                    {
                        bTransmitBuffer[iXmitPointer] = 16;
                        iXmitPointer++;
                    }

                    bTransmitBuffer[iXmitPointer] = b;
                    iXmitPointer++;
                }
            }

            bTransmitBuffer[iXmitPointer] = 3;
            iXmitPointer++;

            await _transport.WriteAsync(bTransmitBuffer, 0, iXmitPointer, cancellationToken).ConfigureAwait(false);

            // allow write buffer to drain if supported
            while (_transport.BytesToWrite > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Initializes the node configuration and transmits the initialization package asynchronously.
        /// </summary>
        /// <remarks>If a configuration for the specified node address already exists, it is replaced with
        /// the new configuration.</remarks>
        /// <param name="UA">The unique address of the node to initialize.</param>
        /// <param name="NodeType">The type of node to configure.</param>
        /// <param name="CT">An optional byte array containing custom configuration data for the node. May be null if no custom data is
        /// required.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous initialization operation.</returns>
        public async Task Init(int UA, NodeType NodeType, byte[] CT = null, CancellationToken cancellationToken = default)
        {
            // add a new config instance. replace whatever was previously there
            _nodeConfigurations[UA] = new NodeConfiguration(UA, NodeType, CT);

            byte[] outputBuffer = BuildInitBuffer(UA, NodeType, CT);
            await TransmitPackage(UA, 'I', outputBuffer, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Builds and returns an initialization buffer for the specified node address and node type.
        /// </summary>
        /// <remarks>The structure and contents of the returned buffer depend on the node type and
        /// configuration. The method may append additional bytes based on the node type to ensure correct
        /// initialization.</remarks>
        /// <param name="UA">The unique address of the node for which to build the initialization buffer.</param>
        /// <param name="nodeType">The type of node that determines the initialization buffer format.</param>
        /// <param name="CT">A byte array containing configuration or control data used in the initialization process.</param>
        /// <returns>A byte array representing the initialization buffer for the specified node and configuration.</returns>
        private byte[] BuildInitBuffer(int UA, NodeType nodeType, byte[] CT)
        {
            var nodeConfig = _nodeConfigurations[UA];
            byte[] outputBuffer = new byte[3]; 

            outputBuffer[0] = nodeConfig.NodeDefinitionParameter;
            outputBuffer[1] = (byte)(_delay / 256);
            outputBuffer[2] = (byte)(_delay - (outputBuffer[1] * 256));

            if (nodeType == NodeType.MAXI24 || nodeType == NodeType.MAXI32)
                outputBuffer = outputBuffer.Concat(GetMaxiInitBytes(nodeConfig.CT)).ToArray();
            else if (nodeType == NodeType.SMINI)
                outputBuffer = outputBuffer.Concat(GetSminiInitBytes(nodeConfig.CT)).ToArray();
            else if (nodeType == NodeType.CPNODE)
                outputBuffer = outputBuffer.Concat(new byte[1] { 0 }).ToArray();

            return outputBuffer;
        }

        /// <summary>
        /// Asynchronously retrieves the current input values for the specified node address.
        /// </summary>
        /// <remarks>This method communicates with the node using a transport layer to request and read its
        /// input values. The operation may be subject to protocol errors if the data stream is malformed. The method
        /// returns as soon as a valid input sequence is received or throws an exception if a protocol violation is
        /// detected.</remarks>
        /// <param name="UA">The unique address of the node for which to retrieve input values.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A byte array containing the input values for the specified node. The length of the array corresponds to the
        /// node's configured input size.</returns>
        /// <exception cref="InvalidOperationException">Thrown if an unexpected byte sequence is encountered during input retrieval, indicating a protocol error.</exception>
        public async Task<byte[]> Inputs(int UA, CancellationToken cancellationToken = default)
        {
            NodeConfiguration nodeConfig = _nodeConfigurations[UA];
            byte[] inputs = new byte[nodeConfig.InputSize];

            while (true)
            {
                _transport.DiscardInBuffer();

                await TransmitPackage(UA, 'P', inputs, cancellationToken).ConfigureAwait(false);

                await TryReadInputsHeader(UA, cancellationToken).ConfigureAwait(false);

                for (int i = 0; i < nodeConfig.InputSize; i++)
                {
                    byte iInByte = await ReceiveByte(UA, cancellationToken).ConfigureAwait(false);

                    if (iInByte == 2)
                        throw new InvalidOperationException("ERROR: No DLE ahead of 2 for UA = " + UA);
                    else if (iInByte == 3)
                        throw new InvalidOperationException("ERROR: No DLE ahead of 3 for UA = " + UA);
                    else if (iInByte == 16)
                        iInByte = await ReceiveByte(UA, cancellationToken).ConfigureAwait(false);

                    inputs[i] = iInByte;
                }

                byte etx = await ReceiveByte(UA, cancellationToken).ConfigureAwait(false);
                if (etx != 3)
                    Console.Error.WriteLine("ERROR: ETX NOT PROPERLY RECEIVED FOR UA = " + UA);

                return inputs;
            }
        }

        /// <summary>
        /// Attempts to read and validate the inputs header for the specified unit address within the configured timeout
        /// period.
        /// </summary>
        /// <remarks>This method performs multiple asynchronous read operations and validates the received
        /// header bytes. If the header is not received or is invalid, an exception is thrown rather than returning <see
        /// langword="false"/>.</remarks>
        /// <param name="UA">The unit address to read the inputs header from. Must correspond to a valid device address.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the read operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the inputs
        /// header is successfully read and validated; otherwise, the method throws an exception.</returns>
        /// <exception cref="InvalidDataException">Thrown if the received data does not match the expected header format or unit address.</exception>
        /// <exception cref="TimeoutException">Thrown if the operation does not complete within the configured timeout period.</exception>
        private async Task<bool> TryReadInputsHeader(int UA, CancellationToken cancellationToken)
        {
            var start = System.Diagnostics.Stopwatch.StartNew();

            while (start.ElapsedMilliseconds < _timeoutMs)
            {
                byte iInByte = await ReceiveByte(UA, cancellationToken).ConfigureAwait(false);

                if (iInByte != 2)
                    continue;

                iInByte = await ReceiveByte(UA, cancellationToken).ConfigureAwait(false);
                if (iInByte - 65 != UA)
                    throw new InvalidDataException($"ERROR; Received bad UA = {iInByte}");

                iInByte = await ReceiveByte(UA, cancellationToken).ConfigureAwait(false);
                if (iInByte != 82)
                    throw new InvalidDataException($"Error received not = R for UA = {UA}");

                return true;
            }

            throw new TimeoutException($"Timed out waiting for CMRI STX for UA = {UA}");
        }

        /// <summary>
        /// Transmits the specified output buffer to the node identified by the given UA.
        /// </summary>
        /// <remarks>The method discards any existing output buffer before transmitting the new data. The
        /// operation is performed asynchronously and can be cancelled using the provided cancellation token.</remarks>
        /// <param name="UA">The unique address of the target node to which the output buffer will be sent.</param>
        /// <param name="OutputBuffer">The buffer containing output data to transmit. The length must match the expected output size for the
        /// specified node.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous transmit operation.</returns>
        /// <exception cref="ArgumentException">Thrown if the length of OutputBuffer does not match the expected output size for the specified UA.</exception>
        public async Task Outputs(int UA, byte[] OutputBuffer, CancellationToken cancellationToken = default)
        {
            var nodeConfig = _nodeConfigurations[UA];

            if (OutputBuffer.Length != nodeConfig.OutputSize)
                throw new ArgumentException($"Output buffer size ({OutputBuffer.Length}) does not match expected size ({nodeConfig.OutputSize}) for UA = {UA}");

            _transport.DiscardOutBuffer(); //TODO: probably remove this. transmit discards the outbuffer
            await TransmitPackage(UA, 'T', OutputBuffer, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Generates an initialization byte array for the Smini device based on the provided CT array, encoding the
        /// number and positions of valid dual lead signals.
        /// </summary>
        /// <remarks>A valid dual lead signal configuration requires that high bits in each CT array
        /// element appear in even-length consecutive groups. Odd-length groups are considered invalid and will cause an
        /// exception.</remarks>
        /// <param name="CT">The CT array representing card signal configurations. Each element encodes signal positions using bitwise
        /// flags. Cannot be null.</param>
        /// <returns>A byte array where the first byte indicates the number of valid dual lead signals, followed by the original
        /// CT array if any valid signals are present.</returns>
        /// <exception cref="ArgumentException">Thrown if any element in the CT array contains an invalid dual lead signal configuration.</exception>
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
        /// Builds a byte array that encodes the length and contents of the specified CT array for use in Maxi
        /// initialization.
        /// </summary>
        /// <param name="CT">The CT array to include in the output buffer. Cannot be null.</param>
        /// <returns>A byte array where the first byte represents the length of the CT array, followed by the contents of CT.</returns>
        private byte[] GetMaxiInitBytes(byte[] CT)
        {
            // build the ct portion of output buffer
            byte[] ctOutputBuffer = new byte[1 + CT.Length];

            // number of cards/ 4, rounded up
            int NS = CT.Length;
            ctOutputBuffer[0] = (byte)NS;

            // copy ct array to output buffer
            CT.CopyTo(ctOutputBuffer, 1);

            return ctOutputBuffer;
        }
    }

    /// <summary>
    /// Represents the configuration settings for a node, including unit address, node type, input and output sizes, and
    /// configuration table parameters.
    /// </summary>
    /// <remarks>The NodeConfiguration class encapsulates the parameters required to initialize and describe a
    /// node's configuration. It determines input and output sizes based on the node type and configuration table. For
    /// SMINI nodes, a default configuration table is used if none is provided. For MAXI24 and MAXI32 nodes, a valid
    /// configuration table must be supplied. This class is intended for internal use and is not thread-safe.</remarks>
    internal sealed class NodeConfiguration
    {
        public int UA { get; }
        public byte NodeDefinitionParameter { get; }
        public int InputSize { get; }
        public int OutputSize { get; }
        public byte[] CT { get; }

        /// <summary>
        /// Initializes a new instance of the NodeConfiguration class with the specified unit address, node type, and
        /// configuration table.
        /// </summary>
        /// <remarks>For SMINI nodes, if ct is null, a default configuration table is used. For MAXI24 and
        /// MAXI32 nodes, ct must be provided and valid. The input and output sizes are determined based on the node
        /// type and configuration table.</remarks>
        /// <param name="ua">The unit address for the node. Must be in the range 0 to 127.</param>
        /// <param name="nodeType">The type of node to configure. Determines input and output sizes and required configuration parameters.</param>
        /// <param name="ct">The configuration table for the node. Required for MAXI node types; may be null for other node types.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if ua is greater than 127.</exception>
        /// <exception cref="ArgumentNullException">Thrown if ct is null or empty when nodeType is MAXI24 or MAXI32.</exception>
        /// <exception cref="ArgumentException">Thrown if nodeType is not a supported value.</exception>
        internal NodeConfiguration(int ua, NodeType nodeType, byte[] ct)
        {
            if (ua > 127)
                throw new ArgumentOutOfRangeException("UA", "Valid UA range is 0-127");
            UA = ua;
            CT = ct;
                        
            switch (nodeType)
            {
                case NodeType.SMINI:
                    NodeDefinitionParameter = (byte)'M';
                    InputSize = 3;
                    OutputSize = 6;

                    CT = CT ?? new byte[] { 0, 0, 0, 0, 0, 0 };
                    break;

                case NodeType.MAXI24:
                case NodeType.MAXI32:
                    NodeDefinitionParameter = nodeType == NodeType.MAXI24 ? (byte)'N' : (byte)'X';

                    if (CT == null || CT.Length == 0)
                        throw new ArgumentNullException("CT", "CT Parameter is required for 24 bit MAXI Nodes");

                    _validateMaxi(ct);

                    var (inputSize, outputSize) = _countIoSize(nodeType, ct);

                    InputSize = inputSize;
                    OutputSize = outputSize;
                    break;

                case NodeType.CPNODE:
                    //TODO:CP Nodes can be configured in a way that impacts input and output size. 
                    NodeDefinitionParameter = (byte)'C';
                    InputSize = 3;
                    OutputSize = 3;
                    break;

                default:
                    throw new ArgumentException($"Unsupported node type: {nodeType}");
            }
        }

        private static void _validateMaxi(byte[] ct)
        {
            bool foundFinalCard = false;
            for (int i = 0; i < ct.Length; i++)
            {
                byte b = ct[i];
                /* bitwise function to ensure slots are not set to both input and output */
                for (int j = 0; j < 8; j += 2) // goes to 8 so we can guarantee a 0 so we don't miss the last digit
                {
                    // checks to see if the bit is set for either an input or output
                    if ((b & 1 << j) != 0 && (b & 1 << j + 1) != 0)
                    {
                        throw new ArgumentException("CT",
                            $"CT array value at index: ${i} with value: ${b} " +
                            $"contains invalid input and output board positions. Slot can not be both input and output");
                    }
                    // if the final card has been found, ensure that there are no additional cards after it. throw if we find another card config
                    else if (foundFinalCard && ((b & 1 << j) == 1 || (b & 1 << j + 1) == 1))
                    {
                        throw new ArgumentException("CT",
                            $"CT array value at index: ${i} with value: ${b} " +
                            $"found card configuration after empty slot");
                    }
                    // this should be after the final card. mark that we have found it and continue ensuring that there are no additional cards after it
                    else if ((b & 1 << j) == 0 && (b & 1 << j + 1) == 0)
                    {
                        foundFinalCard = true;
                    }
                }
            }
        }

        private static (int inputCards, int outputCards) _countIoSize(NodeType nodeType, byte[] ct)
        {
            //TODO: count the inputs and outputs instead 

            int inputSize = 0;
            int outputSize = 0;

            bool finalCard = false;
            for (int i = 0; i < ct.Length && !finalCard; i++)
            {
                // each byte represents a set of 4 cards
                byte b = ct[i];
                for (int j = 0; j < 8; j += 2)
                {
                    if ((b & 1 << j) == 1 && (b & 1 << j + 1) == 0)
                    {
                        // 01 is an input
                        inputSize++;
                    }
                    else if ((b & 1 << j) == 0 && (b & 1 << j + 1) == 1)
                    {
                        // 10 is an output
                        outputSize++;
                    }
                    else
                    {
                        finalCard = true;
                        break;
                    }
                }
            }

            // ... logic to count io size ...
            return (inputSize, outputSize);
        }
    }

    /// <summary>
    /// Specifies the available types of nodes supported by the system.
    /// </summary>
    /// <remarks>Use this enumeration to identify or configure the specific node type when working with
    /// node-related operations. The values represent distinct hardware or logical node variants.</remarks>
    public enum NodeType
    {
        SMINI,
        MAXI24,
        MAXI32,
        CPNODE
    }
}
