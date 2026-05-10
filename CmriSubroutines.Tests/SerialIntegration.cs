using System;
using CmriSubroutines;

namespace CmriSubroutines.Tests
{
    [TestClass]
    [TestCategory("Integration")]
    public sealed class SerialIntegration
    {
        [TestMethod]
        public async Task Serial_OnConfiguredPort_CanInitWriteAndRead()
        {
            if (!IsEnabled())
            {
                Assert.Inconclusive("Set CMRI_RUN_SERIAL_INTEGRATION=1 and CMRI_SERIAL_PORT to run this test against a configured serial transport.");
            }

            string portName = GetPortName();
            int nodeAddress = 0;
            NodeType nodeType = NodeType.MAXI24;
            byte[] ct = [0b00001001];
            byte[] outputs = [255, 255, 255];
            int expectedInputLength = 3;
            byte[]? expectedInputs = null;

            Subroutines subroutines = await Subroutines.CreateSerial(portName).ConfigureAwait(false);

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
            string? value = Environment.GetEnvironmentVariable("CMRI_RUN_SERIAL_INTEGRATION");
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetPortName()
        {
            string? value = Environment.GetEnvironmentVariable("CMRI_SERIAL_PORT");
            return string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException("Set CMRI_SERIAL_PORT to the serial port name before running this test.") : value;
        }
    }
}
