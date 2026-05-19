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
        Task Write(byte[] buffer, CancellationToken cancellationToken = default);
        Task DiscardInBuffer(CancellationToken cancellationToken = default);
        Task DiscardOutBuffer(CancellationToken cancellationToken = default);
        int BytesToRead { get; }
        int BytesToWrite { get; }
    }
}
