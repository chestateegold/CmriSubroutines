using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CmriSubroutines
{
    public class Subroutines
    {
        private System.IO.Ports.SerialPort CommObj;
        private readonly int _maxTries;
        private readonly int _delay;
        private readonly int _maxBuf;

        /// <summary>
        /// Initializes the Serial Port Communications Object with default 1152 baud rate, 1500 max tries, 0 delay and 64 maxbuf
        /// </summary>
        /// <param name="ComPort"></param>
        public Subroutines(int ComPort) : this(ComPort, 1152, 1500, 0, 64)
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
        {
            /* Validate all arguments */
            if (ComPort < 1 || ComPort > 6)
                throw new ArgumentOutOfRangeException("ComPort", "Valid COMPORT range is 1-6");

            if (Baud100 != 96 && Baud100 != 192 && Baud100 != 288 && Baud100 != 576 && Baud100 != 1152)
                throw new ArgumentOutOfRangeException("Baud100", "Valid BAUD100 values are 96, 192, 228, 576 and 1152");

            if (MaxTries <= 0)
                throw new ArgumentOutOfRangeException("MaxTries", "MaxTries must be a positive");

            if (Delay < 0)
                throw new ArgumentOutOfRangeException("Delay", "Delay can not be less than zero");

            if (MaxBuf <= 0)
                throw new ArgumentOutOfRangeException("MaxBuf", "MaxBuf must be a positive");

            _maxTries = MaxTries;
            _delay = Delay;
            _maxBuf = MaxBuf;

            CommObj = new System.IO.Ports.SerialPort();
            if (CommObj.IsOpen)
                CommObj.Close();

            /* SET MScomm1 TO SELECTED PORT */
            //The object name is formatted like "COM4"
            CommObj.PortName = "COM" + ComPort;
            CommObj.BaudRate = Baud100 * 100;  // the system needs the full baud rate
            CommObj.Parity = System.IO.Ports.Parity.None;
            CommObj.DataBits = 8;
            CommObj.StopBits = System.IO.Ports.StopBits.Two;

            /* INITIALIZE REMAINDER OF MSComm1 PROPERTIES */
            CommObj.WriteBufferSize = _maxBuf;
            CommObj.ReadBufferSize = _maxBuf;
            CommObj.Open();
            CommObj.DiscardInBuffer();
            CommObj.DiscardOutBuffer();
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
            byte[] ctOutputBuffer = new byte[1 + CT.Length];

            // number of 2 lead signals
            ctOutputBuffer[0] = (byte)twoLeadSignalCount;

            // copy ct array to output buffer
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

            // number of 2 lead signals
            int NS = CT.Length % 4 == 0 ? CT.Length : CT.Length / 4 + 1;
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
                CommObj.DiscardInBuffer();

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
            CommObj.DiscardOutBuffer();
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

            CommObj.DiscardOutBuffer();

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
            CommObj.Write(bTransmitBuffer, 0, iXmitPointer);

            while (CommObj.BytesToWrite > 0) // allows buffer to empty if it is taking long         
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
                if (CommObj.BytesToRead > _maxBuf)
                    throw new OverflowException("Node " + UA + " bytes to read is over MaxBuf value of " + _maxBuf);

                if (CommObj.BytesToRead != 0)
                    break;

                tries++;
            } while (tries < _maxTries);

            if (tries == _maxTries)
                throw new TimeoutException("INPUT TRIES EXCEEDED " + _maxTries + " NODE = " + UA + " ABORTING INPUT");

            return (byte)CommObj.ReadByte();
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
