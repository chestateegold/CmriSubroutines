[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace CmriSubroutines.Tests
{
    [TestClass]
    public static class MSTestSettings
    {
        [AssemblyInitialize]
        public static void Initialize(TestContext context)
        {
            // tcp integration tests
            Environment.SetEnvironmentVariable("CMRI_RUN_TCP_INTEGRATION", "0", EnvironmentVariableTarget.Process);

            // serial integration tests
            Environment.SetEnvironmentVariable("CMRI_RUN_SERIAL_INTEGRATION", "0", EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("CMRI_SERIAL_PORT", "5", EnvironmentVariableTarget.Process);
        }
    }
}
