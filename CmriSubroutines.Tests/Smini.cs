using CmriSubroutines;
using CmriSubroutines.Transports;

namespace CmriSubroutines.Tests
{
    [TestClass]
    public sealed class Smini
    {
        [TestMethod]
        public async Task Init_WithSmini_WritesExpectedTransmitBuffer()
        {
            MemoryTransport transport = new();
            Subroutines subroutines = new(transport, 3000, 0, 64);

            await subroutines.Init(0, NodeType.SMINI);

            byte[] expected =
            [
                255, 255, 2, 65, 73, 77, 0, 0, 0, 3
            ];

            CollectionAssert.AreEqual(expected, transport.GetWrittenWrite(0));
        }
        [TestMethod]
        public async Task Inputs_WithSmini_WritesExpectedTransmitBuffer()
        {
            MemoryTransport transport = new([255, 255, 2, 65, 82, 0, 0, 0, 3]);
            Subroutines subroutines = new(transport, 3000, 0, 64);

            await subroutines.Init(0, NodeType.SMINI);

            await subroutines.Inputs(0);

            byte[] expected =
            [
                255, 255, 2, 65, 80, 3
            ];

            // only reading the second write, which is the request for the inputs
            CollectionAssert.AreEqual(expected, transport.GetWrittenWrite(1));
        }
        [TestMethod]
        public async Task Inputs_WithSmini_TimeoutOnReceiveByte()
        {
            MemoryTransport transport = new();
            Subroutines subroutines = new(transport, 250, 0, 64);

            await subroutines.Init(0, NodeType.SMINI);

            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await subroutines.Inputs(0);
            });
        }
        [TestMethod]
        public async Task Outputs_WithSmini_WritesExpectedTransmitBuffer()
        {
            MemoryTransport transport = new();
            Subroutines subroutines = new(transport, 3000, 0, 64);

            await subroutines.Init(0, NodeType.SMINI);

            await subroutines.Outputs(0, [255, 1, 1, 5, 5, 8]);

            byte[] expected =
            [
                255,255,2,65,84,
                255, 1, 1, 5, 5, 8, // these are the outputs we are expecting and match whats above
                3
            ];

            // only reading the second write, which is the outputs
            CollectionAssert.AreEqual(expected, transport.GetWrittenWrite(1));
        }
        [TestMethod]
        public async Task Outputs_WithSminiAndEscapes_WritesExpectedTransmitBuffer()
        {
            MemoryTransport transport = new();
            Subroutines subroutines = new(transport, 3000, 0, 64);

            await subroutines.Init(0, NodeType.SMINI);

            await subroutines.Outputs(0, [2, 3, 16, 5, 5, 8]);

            byte[] expected =
            [
                255,255,2,65,84,
                // these are the outputs we are expecting and match whats above. 2,3 and 16 are special and have to be escaped with 16 
                16,2, 16,3, 16,16, 5, 5, 8,
                3
            ];

            // only reading the second write, which is the outputs
            CollectionAssert.AreEqual(expected, transport.GetWrittenWrite(1));
        }

        [TestMethod]
        public async Task Outputs_WithSminiAndCt_WritesExpectedTransmitBuffer()
        {
            MemoryTransport transport = new();
            Subroutines subroutines = new(transport, 3000, 0, 64);

            await subroutines.Init(0, NodeType.SMINI, [3, 0, 0, 0, 0, 0]);

            byte[] expected =
            [
                255, 255, 2, 65, 73, 77, 0, 0, 1, 16, 3, 0, 0, 0, 0, 0, 3
            ];

            var written = transport.GetWrittenWrite(0);
            CollectionAssert.AreEqual(expected, written);
        }
    }
}
