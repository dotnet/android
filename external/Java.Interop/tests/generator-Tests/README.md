
These tests are not intuitive at all. So we need some documentation here.

These tests compare outcomes from generator to the "expected" outcomes.

It does not work together with class-parse, so you cannot pass any jars
nor java sources, which is a pain point (but those who created these tests
didn't care).

Integration tests generate `XAJavaInterop1` bindings and compare them with
the baselines in `expected.xaji`. API descriptions, metadata, and additional
compilation support sources live in `TestInputs`.

Tests that use `BaseGeneratorTest` are organized as:

./BaseGeneratorTest.cs - sets up generation and compilation options.
./Compiler.cs - implements C# compilation with Roslyn.
./(others).cs - the actual `TestFixture`s.

What those tests do are:

- invoke class-parse (and perhaps jar2xml), to generate XML API inputs to generator.
- invoke generator, to generate comparable sources.
- optionally invoke csc to see if it builds.

`BaseGeneratorTest` takes the arguments below,

- outputRelativePath - path to generator output subdir
- apiDescriptionFile - path to the input API XML under `TestInputs`.
- expectedRelativePath - path to the "expected" file generation.
- additionalSupportPaths - path to additional compilation items.

The test outputs are generated to `out.xaji`.

When creating a new test, add its inputs to `TestInputs`, generate the
results in `out.xaji`, and review them before copying the expected sources
to `expected.xaji`.
