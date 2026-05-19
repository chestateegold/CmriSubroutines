# Copilot Instructions

## Project Guidelines
- Repository preference: because CmriSubroutines has not been released yet, API-breaking changes are acceptable when they improve the design; do not preserve public APIs solely for compatibility unless the user says otherwise.

## Target Frameworks
- Support both .NET 10 and .NET Framework when feasible by multi-targeting (e.g., <TargetFrameworks>net10.0;net48</TargetFrameworks>).
- Prefer net48 as the default .NET Framework target; choose net472 only when explicit need exists to support environments that cannot upgrade to 4.8.
- When minimizing multi-target complexity while preserving broad compatibility, target netstandard2.0 alongside net10.0 (e.g., net10.0;netstandard2.0).
- Verify required APIs on each target and use conditional compilation, runtime checks, or reference shims for target-specific differences.
- Run CI and packaging tests for each target to ensure compatibility and catch divergences early.