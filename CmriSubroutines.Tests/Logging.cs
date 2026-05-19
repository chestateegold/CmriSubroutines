using System.Linq;
using CmriSubroutines;
using CmriSubroutines.Transports;
using Microsoft.Extensions.Logging;

namespace CmriSubroutines.Tests
{
    [TestClass]
    public sealed class Logging
    {
        [TestMethod]
        public async Task CreateMemory_WithLogger_LogsInitializationAndOpen()
        {
            TestLogger logger = new();

            Subroutines subroutines = await Subroutines.CreateMemory(timeoutMs: 3000, delay: 0, logger: logger).ConfigureAwait(false);

            Assert.IsTrue(logger.Entries.Any(entry => entry.Level == LogLevel.Information && entry.Message.Contains("Subroutines initialized")));
            Assert.IsTrue(logger.Entries.Any(entry => entry.Level == LogLevel.Debug && entry.Message.Contains("Memory transport opened")));
            Assert.IsNotNull(subroutines);
        }

        [TestMethod]
        public async Task Init_AndOutputs_WithLogger_LogsTransportActivity()
        {
            TestLogger logger = new();
            MemoryTransport transport = new(logger);
            await transport.Open().ConfigureAwait(false);

            Subroutines subroutines = new(transport, 3000, 0, logger);

            await subroutines.Init(0, NodeType.SMINI).ConfigureAwait(false);
            await subroutines.Outputs(0, [0, 0, 0, 0, 0, 0]).ConfigureAwait(false);

            Assert.IsTrue(logger.Entries.Any(entry => entry.Level == LogLevel.Trace && entry.Message.Contains("Writing")));
            Assert.IsTrue(logger.Entries.Any(entry => entry.Level == LogLevel.Debug && entry.Message.Contains("opened")));
        }
    }

    internal sealed class TestLogger : ILogger
    {
        private readonly List<LogEntry> _entries = new();

        public IReadOnlyList<LogEntry> Entries => _entries;

        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            _entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception), exception));
        }

        internal sealed class LogEntry
        {
            public LogEntry(LogLevel level, EventId eventId, string message, Exception exception)
            {
                Level = level;
                EventId = eventId;
                Message = message;
                Exception = exception;
            }

            public LogLevel Level { get; }
            public EventId EventId { get; }
            public string Message { get; }
            public Exception Exception { get; }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose()
            {
            }
        }
    }
}
