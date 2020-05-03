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
        private readonly int MAXTRIES = 1500;
        private readonly int DELAY = 0;
        private readonly int MAXBUF = 50;

        /// <summary>
        /// Initializes the CommObj
        /// </summary>
        /// <param name="COMPORT">COMPORT Number</param>
        /// <param name="BAUD100">Baud rate / 100. Default 1152</param>
        public Subroutines(int COMPORT, int BAUD100 = 1152)
        {
            CommObj = new System.IO.Ports.SerialPort();

            /* Validate all arguments */
            if (COMPORT < 1 || COMPORT > 6)
                throw new ArgumentOutOfRangeException("COMPORT", "Valid COMPORT range is 1-6");

            if (BAUD100 != 96 && BAUD100 != 192 && BAUD100 != 288 && BAUD100 != 576 && BAUD100 != 1152)
                throw new ArgumentOutOfRangeException("BAUD100", "Valid BAUD100 values are 96, 192, 228, 576 and 1152");

            if (MAXBUF < 1 || MAXBUF > 262)
                throw new ArgumentOutOfRangeException("MAXBUF", "Valid MAXBUF range is 1 through 262");

            if (CommObj.IsOpen)
                CommObj.Close();

            /* SET MScomm1 TO SELECTED PORT */
            //The object name is formatted like "COM4"
            CommObj.PortName = "COM" + COMPORT;
            CommObj.BaudRate = BAUD100 * 100;  // the system needs the full baud rate
            CommObj.Parity = System.IO.Ports.Parity.None;
            CommObj.DataBits = 8;
            CommObj.StopBits = System.IO.Ports.StopBits.Two;

            /* INITIALIZE REMAINDER OF MSComm1 PROPERTIES */
            CommObj.WriteBufferSize = MAXBUF;
            CommObj.ReadBufferSize = MAXBUF;
            CommObj.Open();
            CommObj.DiscardInBuffer();
            CommObj.DiscardOutBuffer();
        }

        /// <summary>
        /// initializes specified smini node
        /// </summary>        
        /// <param name="UA">Usic Address</param>
        /// <param name="DL">Delay between bytes being sent back to the PC from the node</param>
        /// <param name="MAXTRIES"></param>
        public void INIT(int UA)
        {
            byte[] iOutputBuffer = new byte[4];

            if (UA > 127)
                throw new ArgumentOutOfRangeException("UA", "Valid UA range is 0-127");

            // **DEFINE INITIALIZATION MESSAGE PARAMETERS
            iOutputBuffer[0] = (byte)'M'; // smini code
            iOutputBuffer[1] = (byte)(DELAY / 256);
            iOutputBuffer[2] = (byte)(DELAY - (iOutputBuffer[1] * 256));
            iOutputBuffer[3] = 0; // number of 2 lead signals

            TransmitPackage(UA, 'I', iOutputBuffer);
        }

        /// <summary>
        /// Gets all inputs from smini node
        /// </summary>
        /// <param name="UA"></param>
        /// <param name="MAXTRIES"></param>
        /// <param name="MAXBUF"></param>
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
                TransmitPackage(UA, 'P', inputs);

                // loop to get start of transmission (stx)
                bool stx = false;
                while (!stx)
                {
                    iInByte = iReceiveByte(UA, MAXTRIES, MAXBUF);

                    if (iInByte != 2) // this message is not the start of the transmission, retry
                        continue;

                    // now checking for the UA
                    iInByte = iReceiveByte(UA, MAXTRIES, MAXBUF);
                    if (iInByte - 65 != UA)
                    {
                        Console.WriteLine("ERROR; Received bad UA = " + iInByte);
                        break; // this has returned the wrong UA, repol.
                    }

                    // check that the message is an 'R' message
                    iInByte = iReceiveByte(UA, MAXTRIES, MAXBUF);
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
                        iInByte = iReceiveByte(UA, MAXTRIES, MAXBUF);

                        if (iInByte == 2)
                            throw new InvalidOperationException("ERROR: No DLE ahead of 2 for UA = " + UA);
                        else if (iInByte == 3)
                            throw new InvalidOperationException("ERROR: No DLE ahead of 3 for UA = " + UA);
                        else if (iInByte == 16) // this is the escape character
                            iInByte = iReceiveByte(UA, MAXTRIES, MAXBUF);

                        inputs[i] = iInByte;
                    }

                    // check for ETX
                    iInByte = iReceiveByte(UA, MAXTRIES, MAXBUF);
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
        /// <param name="iOutputBuffer"></param>
        public void OUTPUTS(int UA, byte[] iOutputBuffer)
        {
            // should be some validation here
            CommObj.DiscardOutBuffer();
            TransmitPackage(UA, 'T', iOutputBuffer); // 84 is message type "T"
        }

        /// <summary>
        /// Transmits outputs to a specified node
        /// </summary>
        /// <param name="CommObj">Comm object used to communivate with nodes</param>
        /// <param name="UA">USIC Address of node</param>
        /// <param name="iMessageType"></param>
        /// <param name="iOutputBuffer">Data to be output</param>
        private void TransmitPackage(int UA, int iMessageType, byte[] iOutputBuffer)
        {
            // buffer that heads to node
            byte[] bTransmitBuffer = new byte[80];

            // pointer for transmit buffer

            bTransmitBuffer[0] = 255;
            bTransmitBuffer[1] = 255;
            bTransmitBuffer[2] = 2;
            bTransmitBuffer[3] = (byte)(UA + 65);
            bTransmitBuffer[4] = (byte)iMessageType;

            int iXmitPointer = 5; // transmit buffer begins at 6th byte, first 5 are header info

            CommObj.DiscardOutBuffer();

            /* Write data from output buffer to transmit buffer. */
            if (iMessageType != 80) // 80 is a poll request, head to end message
            {
                foreach (byte b in iOutputBuffer)
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

        private byte iReceiveByte(int iMaxTries, int UA, int MaxBuf)
        {
            int tries = 0;
            do
            {
                if (CommObj.BytesToRead > MaxBuf)
                    throw new OverflowException("Node " + UA + " bytes to read is over MaxBuf value of " + MaxBuf);

                if (CommObj.BytesToRead != 0)
                    break;

                tries++;
            } while (tries < iMaxTries);

            if (tries == iMaxTries)
                throw new TimeoutException("INPUT TRIES EXCEEDED " + iMaxTries + " NODE = " + UA + " ABORTING INPUT");

            return (byte)CommObj.ReadByte();
        }
    }
}
