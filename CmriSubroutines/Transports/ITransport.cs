using System;
using System.Threading;
using System.Threading.Tasks;

namespace CmriSubroutines.Transports
{
    public interface ITransport : IDisposable
    { 
        Task Open(CancellationToken cancellationToken = default);
        Task Close(CancellationToken cancellationToken = default);
        Task<int> ReadByte(CancellationToken cancellationToken = default);
        Task<int> Read(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default);
        Task Write(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default);
        Task DiscardInBuffer(CancellationToken cancellationToken = default);
        Task DiscardOutBuffer(CancellationToken cancellationToken = default);
        int BytesToRead { get; }
        int BytesToWrite { get; }
        int ReadBufferSize { get; set; }
        int WriteBufferSize { get; set; }
        // Read/Write timeouts in milliseconds. Transport implementations should honor these values.
        int ReadTimeoutMs { get; set; }
        int WriteTimeoutMs { get; set; }
    }
}
