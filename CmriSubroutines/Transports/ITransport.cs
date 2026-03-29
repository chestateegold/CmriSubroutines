using System;
using System.Threading;
using System.Threading.Tasks;

namespace CmriSubroutines.Transports
{
    public interface ITransport : IDisposable
    {
        void Open();
        void Close();
        Task OpenAsync(CancellationToken cancellationToken = default);
        Task CloseAsync(CancellationToken cancellationToken = default);
        int ReadByte();
        int Read(byte[] buffer, int offset, int count);
        Task<int> ReadByteAsync(CancellationToken cancellationToken = default);
        Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default);
        void Write(byte[] buffer, int offset, int count);
        Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default);
        void DiscardInBuffer();
        void DiscardOutBuffer();
        int BytesToRead { get; }
        int BytesToWrite { get; }
        int ReadBufferSize { get; set; }
        int WriteBufferSize { get; set; }
        // Read/Write timeouts in milliseconds. Transport implementations should honor these values.
        int ReadTimeoutMs { get; set; }
        int WriteTimeoutMs { get; set; }
    }
}
