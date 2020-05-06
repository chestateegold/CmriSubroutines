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
        private readonly int _maxTries = 1500;
        private readonly int _delay = 0;
        private readonly int _maxBuf = 64;

        /// <summary>
        /// Initializes the Serial Port Communications Object
        /// </summary>
        /// <param name="ComPort">COMPORT Number</param>
        /// <param name="Baud100">Baud rate / 100. Default 1152</param>
        public Subroutines(int ComPort, int Baud100 = 1152)
        {
            CommObj = new System.IO.Ports.SerialPort();

            /* Validate all arguments */
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
        /// initializes specified smini node
        /// </summary>        
        /// <param name="UA">Usic Address</param>
        public void INIT(int UA)
        {
            byte[] iOutputBuffer = new byte[4];

            if (UA > 127)
                throw new ArgumentOutOfRangeException("UA", "Valid UA range is 0-127");

            // **DEFINE INITIALIZATION MESSAGE PARAMETERS
            iOutputBuffer[0] = (byte)'M'; // smini code
            iOutputBuffer[1] = (byte)(_delay / 256);
            iOutputBuffer[2] = (byte)(_delay - (iOutputBuffer[1] * 256));
            iOutputBuffer[3] = 0; // number of 2 lead signals

            transmitPackage(UA, 'I', iOutputBuffer);
        }

        /// <summary>
        /// Gets all inputs from smini node
        /// </summary>
        /// <param name="UA"></param>
        /// <returns></returns>
        public byte[] INPUTS(int UA)
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
        public void OUTPUTS(int UA, byte[] OutputBuffer)
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
                    throw new OverflowException("Node " + UA + " bytes to read is over MaxBuf value of " + MaxBuf);

                if (CommObj.BytesToRead != 0)
                    break;

                tries++;
            } while (tries < _maxTries);

            if (tries == _maxTries)
                throw new TimeoutException("INPUT TRIES EXCEEDED " + _maxTries + " NODE = " + UA + " ABORTING INPUT");

            return (byte)CommObj.ReadByte();
        }
    }
}
