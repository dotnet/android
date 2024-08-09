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
[android]: https://github.com/xamarin/xamarin-android

## Building

- The `main` branch is configured to build with .NET 7, available [here][net-7].
- The [`release/6.0.3xx`][net-6] branch is configured to build with .NET 6.

`Java.Interop.sln` must first run some "preparatory" tasks before it can be built:

```console
dotnet build -t:Prepare
```

Once `Java.Interop.sln` has been prepared, it can be built in Visual Studio 2022 or with `dotnet`:

```
dotnet build
```

[net-7]: https://dotnet.microsoft.com/en-us/download/dotnet/7.0
[net-6]: https://github.com/dotnet/java-interop/tree/release/6.0.3xx

Additional build options are documented [here][build-configuration].

[build-configuration]: /Documentation/BuildConfiguration.md

## Feedback and Contributing

This project welcomes issues and PRs.

  - File an issue in [GitHub Issues](https://github.com/xamarin/xamarin-android/issues/new/choose).
  - Discuss development and design on [Discord](https://aka.ms/dotnet-discord). [![Discord](https://img.shields.io/badge/chat-on%20discord-brightgreen)](https://aka.ms/dotnet-discord)
  - Coding style is outlined in [Coding Guidelines](http://www.mono-project.com/community/contributing/coding-guidelines/).

## License

Copyright (c) .NET Foundation Contributors. All rights reserved.
Licensed under the [MIT](LICENSE) License.
