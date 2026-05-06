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
            Subroutines subroutines = new(transport, 3000, 0, 64);

            await subroutines.Init(0, NodeType.MAXI24);

            byte[] expected =
            [
                255, 255, 2, 65, 73, 77, 0, 0, 0, 3
            ];

            CollectionAssert.AreEqual(expected, transport.GetWrittenWrite(0));
        }
    }
}
