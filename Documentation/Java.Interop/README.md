# Java.Interop

**Java.Interop** provides .NET bindings for the [Java Native Interface][jni]
and code generators for Java-to-.NET calls. It ships with [.NET for Android][android]
and can be built from source for other Java VMs.

See [Motivation][motivation] and [Architecture][architecture] for more context.

[jni]: http://docs.oracle.com/javase/8/docs/technotes/guides/jni/spec/jniTOC.html
[motivation]: Motivation.md
[architecture]: Architecture.md
[android]: https://github.com/dotnet/android

## Building

Standalone tools and tests target [.NET 11][net-11] and do not require a locally
built Android SDK. For the Android product build, follow the repository's
[build instructions](../README.md#building-from-source).

Libraries and tools live in `src/`, tooling tests in `tests/`, and the JNI
generator in `build-tools/java-interop-jnienv-gen/`. Build from the repository root:

```
dotnet build Java.Interop.slnx
```

Native ABI types use [`ExtendedLayout`][extended-layout]: [`CUnion`][extended-layout-kind]
for `JniArgumentValue` and `CStruct` for native structs. These layouts require
.NET 11 or later compiler and runtime support.

[net-11]: https://dotnet.microsoft.com/en-us/download/dotnet/11.0
[extended-layout]: https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.extendedlayoutattribute?view=net-11.0
[extended-layout-kind]: https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.extendedlayoutkind?view=net-11.0

See [Build Configuration][build-configuration] for additional options.

[build-configuration]: BuildConfiguration.md

## Feedback and Contributing

This project welcomes issues and PRs.

  - File an issue in [GitHub Issues](https://github.com/dotnet/android/issues/new/choose).
  - Discuss development and design on [Discord](https://aka.ms/dotnet-discord). [![Discord](https://img.shields.io/badge/chat-on%20discord-brightgreen)](https://aka.ms/dotnet-discord)
  - Coding style is outlined in [Coding Guidelines](http://www.mono-project.com/community/contributing/coding-guidelines/).

## License

Copyright (c) .NET Foundation Contributors. All rights reserved.
Licensed under the [MIT](LICENSE) License.
