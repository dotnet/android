# Java.Interop

**Java.Interop** is a binding of the [Java Native Interface][jni] for use from
managed languages such as C#, and an associated set of code generators to
allow Java code to invoke managed code.

This allows one to bridge code running on .NET's CLR and code running on a Java VM.

Note this does not mean that one can run Java code on .NET, or vice-versa.

**Java.Interop** currently does not ship independently.  It is shipped as part of Microsoft's
[.NET for Android][android] product, available via Visual Studio or .NET 6+.  However, it is designed
to be fully independent of Android and should be usable by other Java implementations.
For other uses, please compile and distribute from source.

Some additional context for this project is documented in the [Motivation][motivation]
and [Architecture][architecture] pages.

[jni]: http://docs.oracle.com/javase/8/docs/technotes/guides/jni/spec/jniTOC.html
[motivation]: /Documentation/Motivation.md
[architecture]: /Documentation/Architecture.md
[android]: https://github.com/dotnet/android

## Building

- The `main` branch is configured to build with .NET 11, available [here][net-11].
- The [`release/6.0.3xx`][net-6] branch is configured to build with .NET 6.

`JniArgumentValue` uses [`ExtendedLayout`][extended-layout] with
[`ExtendedLayoutKind.CUnion`][extended-layout-kind] to match JNI's native
`jvalue` union. This requires a compiler and runtime supporting .NET 11
extended layout. The local proof of concept targets .NET 11; adoption in
.NET for Android is intended for the .NET 12 release.

`Java.Interop.slnx` must first run some "preparatory" tasks before it can be built:

```console
dotnet build -t:Prepare
```

Once `Java.Interop.slnx` has been prepared, it can be built in Visual Studio 2022 or with `dotnet`:

```
dotnet build
```

[net-11]: https://dotnet.microsoft.com/en-us/download/dotnet/11.0
[net-6]: https://github.com/dotnet/java-interop/tree/release/6.0.3xx
[extended-layout]: https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.extendedlayoutattribute?view=net-11.0
[extended-layout-kind]: https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.extendedlayoutkind?view=net-11.0

Additional build options are documented [here][build-configuration].

[build-configuration]: /Documentation/BuildConfiguration.md

## Feedback and Contributing

This project welcomes issues and PRs.

  - File an issue in [GitHub Issues](https://github.com/dotnet/android/issues/new/choose).
  - Discuss development and design on [Discord](https://aka.ms/dotnet-discord). [![Discord](https://img.shields.io/badge/chat-on%20discord-brightgreen)](https://aka.ms/dotnet-discord)
  - Coding style is outlined in [Coding Guidelines](http://www.mono-project.com/community/contributing/coding-guidelines/).

## License

Copyright (c) .NET Foundation Contributors. All rights reserved.
Licensed under the [MIT](LICENSE) License.
