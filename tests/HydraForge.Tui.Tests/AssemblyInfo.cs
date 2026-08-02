using Xunit;

// AnsiConsole.Console is a shared global mutable singleton — any test that reassigns it
// (BoardRendererTests, via Spectre.Console.Testing.TestConsole) races with every other
// test in the assembly under xUnit's default cross-class parallelization, producing
// nondeterministic render output (wrong line counts) rather than a clean failure.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
