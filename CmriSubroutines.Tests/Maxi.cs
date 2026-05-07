using CmriSubroutines.Transports;
using System;
using System.Collections.Generic;
using System.Text;

namespace CmriSubroutines.Tests
{
    [TestClass]
    public sealed class Maxi
    {
        [TestMethod]
        public async Task Init_WithMaxi_WritesExpectedTransmitBuffer()
        {
            MemoryTransport transport = new();
            await transport.Open();

            Subroutines subroutines = new(transport, 3000, 0, 64);

            await subroutines.Init(0, NodeType.MAXI24, [0b00000101]);

            byte[] expected =
            [
                255, 255, 2, 65, 73, 78, 0, 0, 1, 5, 3
            ];

            CollectionAssert.AreEqual(expected, transport.GetWrittenWrite(0));
        }
        [TestMethod]
        public async Task Init_WithMaxiWithBadCt_CardAfterEmptySlot_ThrowsArgumentException()
        {
            MemoryTransport transport = new();
            await transport.Open();

            Subroutines subroutines = new(transport, 3000, 0, 64);

            var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await subroutines.Init(0, NodeType.MAXI24, [0b01000101]);
            });

            Assert.AreEqual("CT array value at index: 0 with value: 69 (0b01000101) found card configuration after empty slot", ex.Message);
        }
        [TestMethod]
        public async Task Init_WithMaxiWithBadCt_CardIsInputAndOutput_ThrowsArgumentException()
        {
            MemoryTransport transport = new();
            await transport.Open();

            Subroutines subroutines = new(transport, 3000, 0, 64);

            var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await subroutines.Init(0, NodeType.MAXI24, [0b00000011]);
            });

            Assert.AreEqual("CT array value at index: 0 with value: 3 (0b00000011) contains invalid input and output board positions. Slot can not be both input and output", ex.Message);
        }
    }
}
