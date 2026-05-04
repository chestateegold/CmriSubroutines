using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using CmriSubroutines.Transports;

//TODO: don't have both sync and async versions of everything.
namespace CmriSubroutines
{
    /// <summary>
    /// Represents a CMRI connection. One Subroutines instance = one CMRI bus.
    /// </summary>
    public class Subroutines
    {
        private Dictionary<int, NodeConfiguration> _nodeConfigurations = new Dictionary<int, NodeConfiguration>();
        private ITransport _transport;
        private readonly int _timeoutMs;
        private readonly int _delay;
        private readonly int _maxBuf;

        /// <summary>
        /// Creates a <see cref="Subroutines"/> instance backed by a serial transport using a COM port number.
        /// </summary>
        public static Subroutines CreateSerial(int comPort, int baud100 = 1152, int timeoutMs = 3000, int delay = 0, int maxBuf = 64)
        {
            return new Subroutines(new SerialTransport(comPort, baud100, maxBuf), timeoutMs, delay, maxBuf);
        }

        /// <summary>
        /// Creates a <see cref="Subroutines"/> instance backed by a serial transport using a platform-specific port name.
        /// </summary>
        public static Subroutines CreateSerial(string portName, int baud100 = 1152, int timeoutMs = 3000, int delay = 0, int maxBuf = 64)
        {
            return new Subroutines(new SerialTransport(portName, baud100, maxBuf), timeoutMs, delay, maxBuf);
        }

        /// <summary>
        /// Creates a <see cref="Subroutines"/> instance backed by a TCP transport.
        /// </summary>
        public static Subroutines CreateTcp(string host, int port, int timeoutMs = 3000, int delay = 0, int maxBuf = 64)
        {
            return new Subroutines(new TcpTransport(host, port), timeoutMs, delay, maxBuf);
        }

        /// <summary>
        /// Creates a <see cref="Subroutines"/> instance backed by an in-memory transport.
        /// </summary>
        public static Subroutines CreateMemory(IEnumerable<byte> initialReadBuffer = null, int timeoutMs = 3000, int delay = 0, int maxBuf = 64)
        {
            return new Subroutines(initialReadBuffer == null ? new MemoryTransport() : new MemoryTransport(initialReadBuffer), timeoutMs, delay, maxBuf);
        }

        /// <summary>
        /// Core constructor that accepts an ITransport.
        /// </summary>
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
        /// Async version of ReceiveByte which waits up to the configured timeout budget and supports cancellation.
        /// </summary>
        private async Task<byte> ReceiveByteAsync(int UA, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_transport.BytesToRead > 0)
                    return (byte)await _transport.ReadByteAsync(cancellationToken).ConfigureAwait(false);

                int timeoutMs = _timeoutMs;
                int elapsed = 0;
                int pollInterval = 10; // ms

                while (elapsed < timeoutMs)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (_transport.BytesToRead > 0)
                        return (byte)await _transport.ReadByteAsync(cancellationToken).ConfigureAwait(false);

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
        /// TransmitPackage async version
        /// </summary>
        private async Task TransmitPackageAsync(int UA, int MessageType, byte[] OutputBuffer, CancellationToken cancellationToken = default)
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
        /// Async Init
        /// </summary>
        public async Task InitAsync(int UA, NodeType NodeType, byte[] CT = null, CancellationToken cancellationToken = default)
        {
            // add a new config instance. replace whatever was previously there
            _nodeConfigurations[UA] = new NodeConfiguration(UA, NodeType, CT);

            byte[] outputBuffer = BuildInitBuffer(UA, NodeType, CT);
            await TransmitPackageAsync(UA, 'I', outputBuffer, cancellationToken).ConfigureAwait(false);
        }

        private byte[] BuildInitBuffer(int UA, NodeType nodeType, byte[] CT)
        {
            var nodeConfig = _nodeConfigurations[UA];
            byte[] outputBuffer = new byte[nodeConfig.OutputSize];

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
        /// Async Inputs
        /// </summary>
        public async Task<byte[]> InputsAsync(int UA, CancellationToken cancellationToken = default)
        {
            NodeConfiguration nodeConfig = _nodeConfigurations[UA];
            byte[] inputs = new byte[nodeConfig.InputSize];

            while (true)
            {
                _transport.DiscardInBuffer();

                await TransmitPackageAsync(UA, 'P', inputs, cancellationToken).ConfigureAwait(false);

                await TryReadInputsHeaderAsync(UA, cancellationToken).ConfigureAwait(false);

                for (int i = 0; i < nodeConfig.InputSize; i++)
                {
                    byte iInByte = await ReceiveByteAsync(UA, cancellationToken).ConfigureAwait(false);

                    if (iInByte == 2)
                        throw new InvalidOperationException("ERROR: No DLE ahead of 2 for UA = " + UA);
                    else if (iInByte == 3)
                        throw new InvalidOperationException("ERROR: No DLE ahead of 3 for UA = " + UA);
                    else if (iInByte == 16)
                        iInByte = await ReceiveByteAsync(UA, cancellationToken).ConfigureAwait(false);

                    inputs[i] = iInByte;
                }

                byte etx = await ReceiveByteAsync(UA, cancellationToken).ConfigureAwait(false);
                if (etx != 3)
                    Console.Error.WriteLine("ERROR: ETX NOT PROPERLY RECEIVED FOR UA = " + UA);

                return inputs;
            }
        }

        private async Task<bool> TryReadInputsHeaderAsync(int UA, CancellationToken cancellationToken)
        {
            var start = System.Diagnostics.Stopwatch.StartNew();

            while (start.ElapsedMilliseconds < _timeoutMs)
            {
                byte iInByte = await ReceiveByteAsync(UA, cancellationToken).ConfigureAwait(false);

                if (iInByte != 2)
                    continue;

                iInByte = await ReceiveByteAsync(UA, cancellationToken).ConfigureAwait(false);
                if (iInByte - 65 != UA)
                    throw new InvalidDataException($"ERROR; Received bad UA = {iInByte}");

                iInByte = await ReceiveByteAsync(UA, cancellationToken).ConfigureAwait(false);
                if (iInByte != 82)
                    throw new InvalidDataException($"Error received not = R for UA = {UA}");
            }

            throw new TimeoutException($"Timed out waiting for CMRI STX for UA = {UA}");
        }

        /// <summary>
        /// Async Outputs
        /// </summary>
        public async Task OutputsAsync(int UA, byte[] OutputBuffer, CancellationToken cancellationToken = default)
        {
            var nodeConfig = _nodeConfigurations[UA];

            if (OutputBuffer.Length != nodeConfig.OutputSize)
                throw new ArgumentException($"Output buffer size ({OutputBuffer.Length}) does not match expected size ({nodeConfig.OutputSize}) for UA = {UA}");

            _transport.DiscardOutBuffer();
            await TransmitPackageAsync(UA, 'T', OutputBuffer, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Initializes specified node.
        /// </summary>
        /// <param name="UA">Node Address</param>
        /// <param name="NodeType"></param>
        public void Init(int UA, NodeType NodeType)
        {
            InitAsync(UA, NodeType, new byte[] { 0, 0, 0, 0, 0, 0 }).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Initializes specified node with CT parameter.
        /// </summary>
        /// <param name="UA">Node Address</param>
        /// <param name="NodeType"></param>
        /// <param name="CT">Card type array for MAXI nodes and SMINI 2 lead signals</param>
        public void Init(int UA, NodeType NodeType, byte[] CT)
        {
            InitAsync(UA, NodeType, CT).GetAwaiter().GetResult();
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
            return InputsAsync(UA).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Sends outputs to a specified node
        /// </summary>        
        /// <param name="UA"></param>
        /// <param name="OutputBuffer"></param>
        public void Outputs(int UA, byte[] OutputBuffer)
        {
            OutputsAsync(UA, OutputBuffer).GetAwaiter().GetResult();
        }
    }

    internal sealed class NodeConfiguration
    {
        public int UA { get; }
        public NodeType NodeType { get; }
        public byte NodeDefinitionParameter { get; }
        public int InputSize { get; }
        public int OutputSize { get; }
        public byte[] CT { get; }

        public int Cards { get; }

        internal NodeConfiguration(int ua, NodeType nodeType, byte[] ct)
        {
            if (ua > 127)
                throw new ArgumentOutOfRangeException("UA", "Valid UA range is 0-127");
            UA = ua;
            NodeType = nodeType;
            CT = ct;

            //TODO: this is where out logic for determining input and output size based on node type and ct configuration will go.
            //we will need to determine how to handle this for the maxi nodes since the ct configuration can vary widely and impact
            //the input and output sizes in a non uniform way. we may want to consider building out a node configuration mapping
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
                        //TODO: exit both loops
                        finalCard = true;
                        break;
                    }
                }
            }

            // ... logic to count io size ...
            return (inputSize, outputSize);
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

