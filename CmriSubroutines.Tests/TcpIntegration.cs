using System;
using CmriSubroutines;

namespace CmriSubroutines.Tests
{
    [TestClass]
    [TestCategory("Integration")]
    public sealed class TcpIntegration
    {
        [TestMethod]
        public async Task Ser2Net_OnConfiguredPi_CanInitWriteAndRead()
        {
            if (!IsEnabled())
            {
                Assert.Inconclusive("Set CMRI_RUN_TCP_INTEGRATION=1 to run this test against the Raspberry Pi ser2net endpoint.");
            }

            string host = "CmriPi";
            int port = 3333;
            int nodeAddress = 0;
            NodeType nodeType = NodeType.MAXI24;
            byte[] ct = [0b00001001];
            byte[] outputs = [255, 255, 255];
            int expectedInputLength = 3;
            byte[]? expectedInputs = null;

            Subroutines subroutines = await Subroutines.CreateTcp(host, port).ConfigureAwait(false);

            await subroutines.Init(nodeAddress, nodeType, ct).ConfigureAwait(false);
            await subroutines.Outputs(nodeAddress, outputs).ConfigureAwait(false);

            byte[] inputs = await subroutines.Inputs(nodeAddress).ConfigureAwait(false);

            Assert.HasCount(expectedInputLength, inputs);

            if (expectedInputs != null)
            {
                CollectionAssert.AreEqual(expectedInputs, inputs);
            }
        }

        private static bool IsEnabled()
        {
            string? value = Environment.GetEnvironmentVariable("CMRI_RUN_TCP_INTEGRATION");
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}
