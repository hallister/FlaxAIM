using System.Runtime.CompilerServices;

// Unit tests (Tests/FlaxAIM.Tests) and the examples project's engine tests replace internal seams
// (virtual input backend, asset lookup) and inspect internal state.
[assembly: InternalsVisibleTo("FlaxAIM.Tests")]
[assembly: InternalsVisibleTo("FlaxAIMExamples.CSharp")]
