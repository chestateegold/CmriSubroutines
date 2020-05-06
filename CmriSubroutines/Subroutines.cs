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
            _maxTries = MaxTries;
            _delay = Delay;
            _maxBuf = MaxBuf;

            CommObj = new System.IO.Ports.SerialPort();

            /* Validate all arguments */
            //TODO: validate maxtries, delat and maxbuf
            if (ComPort < 1 || ComPort > 6)
                throw new ArgumentOutOfRangeException("COMPORT", "Valid COMPORT range is 1-6");

            if (Baud100 != 96 && Baud100 != 192 && Baud100 != 288 && Baud100 != 576 && Baud100 != 1152)
                throw new ArgumentOutOfRangeException("BAUD100", "Valid BAUD100 values are 96, 192, 228, 576 and 1152");

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
            if (UA > 127)
                throw new ArgumentOutOfRangeException("UA", "Valid UA range is 0-127");

            if ((NodeType == NodeType.MAXI24 || NodeType == NodeType.MAXI32) && (CT == null || CT.Length == 0))
                throw new ArgumentNullException("CT", "CT Parameter is required for MAXI Nodes");

            if (NodeType == NodeType.SMINI && CT.Length != 6)
                throw new ArgumentException("CT", "CT array requires 6 elements for SMINI dual lead signals");

            //TODO: abstract everything after this out. It is different enough to justify it
            byte[] outputBuffer = new byte[4];

            // validate and count the CT array
            int ctCount = 0; // aka NS
            if (NodeType == NodeType.MAXI24 || NodeType == NodeType.MAXI32)
            {
                throw new NotImplementedException("MAXI node not supported. Coming Soon!");
            }
            else if (NodeType == NodeType.SMINI)
            {
                // loop through each card in the CT array to count and validate the locations of 2 lead signals
                for (int i = 0; i < CT.Length; i++)
                {
                    /* bitwise function to check if an odd number of bites are consecutively high.
                     * an odd number of high bits in a row is invalid. */
                    int successiveHighBits = 0;
                    for (int j = 0; j <= 8; j++) // goes to 8 so we can guarantee a 0 so we don't miss the last digit
                    {
                        if ((1 << j & CT[i]) != 0)
                        {
                            successiveHighBits++;
                        }
                        else if (successiveHighBits % 2 == 0)
                        {
                            ctCount += successiveHighBits / 2; // one signal for every 2 high bits in a row
                            successiveHighBits = 0;
                        }
                        else
                        {
                            throw new ArgumentException("CT",
                                $"CT array value at index: ${i} with value: ${CT[i]} " +
                                $"contains invalid dual lead signal positions ");
                        }
                    }
                }
            }

            //TODO: update codes based on node type
            // DEFINE INITIALIZATION MESSAGE PARAMETERS
            outputBuffer[0] = (byte)'M'; // smini code
            outputBuffer[1] = (byte)(_delay / 256);
            outputBuffer[2] = (byte)(_delay - (outputBuffer[1] * 256));

            //TODO this won't be static anymore
            outputBuffer[3] = 0; // number of 2 lead signals

            //TODO will transmit package with whatever is returned from our abstracted method
            transmitPackage(UA, 'I', outputBuffer);
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
                transmitPackage(UA, 'P', inputs);

                // loop to get start of transmission (stx)
                bool stx = false;
                while (!stx)
                {
                    iInByte = receiveByte(UA);

                    if (iInByte != 2) // this message is not the start of the transmission, retry
                        continue;
                    else
                        poll = false;

                    // now checking for the UA
                    iInByte = receiveByte(UA);
                    if (iInByte - 65 != UA)
                    {
                        Console.WriteLine("ERROR; Received bad UA = " + iInByte);
                        break; // this has returned the wrong UA, repol.
                    }

                    // check that the message is an 'R' message
                    iInByte = receiveByte(UA);
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
                        iInByte = receiveByte(UA);

                        if (iInByte == 2)
                            throw new InvalidOperationException("ERROR: No DLE ahead of 2 for UA = " + UA);
                        else if (iInByte == 3)
                            throw new InvalidOperationException("ERROR: No DLE ahead of 3 for UA = " + UA);
                        else if (iInByte == 16) // this is the escape character
                            iInByte = receiveByte(UA);

                        inputs[i] = iInByte;
                    }

                    // check for ETX
                    iInByte = receiveByte(UA);
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
            transmitPackage(UA, 'T', OutputBuffer); // 84 is message type "T"
        }

        /// <summary>
        /// Transmits outputs to a specified node
        /// </summary>
        /// <param name="CommObj">Comm object used to communivate with nodes</param>
        /// <param name="UA">USIC Address of node</param>
        /// <param name="MessageType"></param>
        /// <param name="OutputBuffer">Data to be output</param>
        private void transmitPackage(int UA, int MessageType, byte[] OutputBuffer)
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

        private byte receiveByte(int UA)
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
        MAXI32
    }
}
