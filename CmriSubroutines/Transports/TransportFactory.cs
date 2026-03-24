using System;

namespace CmriSubroutines.Transports
{
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
