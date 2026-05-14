using Xunit;

// All end-to-end tests in this assembly spin up a WebApplicationFactory<Program>
// which calls AddSerilog. Serilog's static ReloadableLogger is frozen the first
// time the host builds, so running two factories concurrently throws
// "The logger is already frozen". Disable parallelization to keep the
// assembly stable; the per-test runtime is small (<1s each).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
