
These tests are not intuitive at all. So we need some documentation here.

These tests compare outcomes from generator to the "expected" outcomes.

It does not work together with class-parse, so you cannot pass any jars
nor java sources, which is a pain point (but those who created these tests
didn't care).

There are two "expected" set of files. One is "expected" and the other is
"expected.ji". They are different per Java.Interop output methods.

The differences between "expected" and "expected.ji" are almost only
annoying, but this test blindly compares those differences. So if you are
going to add tests you will have to duplicate your work twice...

Tests that use `BaseGeneratorTest` are organized as:

./BaseGeneratorTest.cs - sets up generation and compilation options.
./Compiler.cs - implements C# compilation with `CodeDomProvider`.
./(others).cs - the actual `TestFixture`s.

What those tests do are:

- invoke class-parse (and perhaps jar2xml), to generate XML API inputs to generator.
- invoke generator, to generate comparable sources.
- optionally invoke csc to see if it builds.

`BaseGeneratorTest` takes the arguments below,

- outputRelativePath - path to generator output subdir
- apiDescriptionFile - path to the input API XML output. Sadly existing tests
  are organized horribly and they reside in the "expected" directory.
- expectedRelativePath - path to the "expected" file generation.
- additionalSupportPaths - path to additional compilation items.

The test outputs are generated to `out` and `out.ji` directories, per
the generator's output method.

When you are creating a new test, it is easier to once generate results in
those `out` and `out.ji` directories, and copy them as "expected" and
"expected.ji" with required changes (so that they become really expected
contents).
