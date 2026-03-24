using System;

namespace CmriSubroutines.Transports
{
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
}
