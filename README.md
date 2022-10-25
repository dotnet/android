<img src="Documentation/images/banner.png" alt=".NET Android banner" height="145" >

.NET Android
===============

`.NET Android` provides open-source bindings of the Android SDK and tooling for use with
.NET managed languages such as C#. This ships as an optional [.NET workload][net-workload] for .NET 6+ that can 
be updated independently from .NET in order to respond to external dependency updates like new Android
platform and tooling.

While `.NET Android` is an essential part of [MAUI][maui-intro], it is still fully supported to be 
used independently for "native" .NET Android development.

This repository is also home to the classic `Xamarin.Android` product.

[net-workload]: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-workload-install
[maui-intro]: https://learn.microsoft.com/en-us/dotnet/maui/what-is-maui

# Support

`.NET Android` is now part of .NET 6+ and follows the same lifecycle as the [MAUI Support Lifecycle][maui-support-lifecycle].

Classic `Xamarin.Android` support will end on `May 1, 2024` per the [Xamarin Support Policy][xamarin-support-policy]:

> Xamarin support will end on May 1, 2024 for all classic Xamarin SDKs. Android 13 will be the final version classic Xamarin.Android will target.

[maui-support-lifecycle]: https://dotnet.microsoft.com/en-us/platform/support/policy/maui
[xamarin-support-policy]: https://dotnet.microsoft.com/en-us/platform/support/policy/xamarin

# Build Status

| Platform              | Status |
|-----------------------|--------|
| **OSS macOS**         | [![OSS macOS x86_64][oss-macOS-x86_64-icon]][oss-macOS-x86_64-status] |
| **OSS Ubuntu**        | [![OSS Linux/Ubuntu x86_64][oss-ubuntu-x86_64-icon]][oss-ubuntu-x86_64-status] |

[oss-macOS-x86_64-icon]: https://dev.azure.com/xamarin/public/_apis/build/status/xamarin/xamarin-android/Xamarin.Android-OSS?branchName=main&stageName=Mac
[oss-macOS-x86_64-status]: https://dev.azure.com/xamarin/public/_build/latest?definitionId=48&branchName=main&stageName=Mac
[oss-ubuntu-x86_64-icon]: https://dev.azure.com/xamarin/public/_apis/build/status/xamarin/xamarin-android/Xamarin.Android-OSS?branchName=main&stageName=Linux
[oss-ubuntu-x86_64-status]: https://dev.azure.com/xamarin/public/_build/latest?definitionId=48&branchName=main&stageName=Linux

# Downloads

## Current

`.NET Android` ships as a workload through the `dotnet` workload system in [.NET 6+][dotnet-download]. See
the [workload documentation][workload-documentation] for installation commands.

[dotnet-download]: https://dotnet.microsoft.com/en-us/download
[workload-documentation]: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-workload-install

Classic `Xamarin.Android` installers are available here:

| Platform        | Link   |
|-----------------|--------|
| **Commercial Xamarin.Android 13.0 (d17-3)** for Windows+Visual Studio 2022                  | [Download][commercial-d17-3-Windows-x86_64] |
| **Commercial Xamarin.Android 13.0 (d17-3)** for macOS                                       | [Download][commercial-d17-3-macOS-x86_64]   |
| **OSS Xamarin.Android (main)** for Ubuntu\*                                                 | [![OSS Linux/Ubuntu x86_64][oss-ubuntu-x86_64-icon]][oss-ubuntu-x86_64-status] |

*\* Please note that the OSS installer packages are not digitally signed.*

[Previous Releases](Documentation/previous-releases.md) are also available for download.

[commercial-d17-3-Windows-x86_64]:        https://aka.ms/xamarin-android-commercial-d17-3-windows
[commercial-d17-3-macOS-x86_64]:          https://aka.ms/xamarin-android-commercial-d17-3-macos

# Contributing

If you are interested in fixing issues and contributing directly to the code base, please see the following:

  - [How to build and run from source](Documentation/README.md#building-from-source)
  - [The development workflow, and using your build](Documentation/README.md#development-workflow)
  - [Coding Guidelines](http://www.mono-project.com/community/contributing/coding-guidelines/)
  - [Submitting pull requests](https://github.com/xamarin/xamarin-android/wiki/Submitting-Bugs,-Feature-Requests,-and-Pull-Requests#pull-requests)

This project has adopted the code of conduct defined by the Contributor Covenant
to clarify expected behavior in our community. For more information, see the
[.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

# Feedback

  - Ask a question on [Stack Overflow](https://stackoverflow.com/questions/tagged/xamarin.android) or [Microsoft Q&A](https://docs.microsoft.com/en-us/answers/topics/dotnet-android.html).
  - [Request a new feature or vote for popular feature requests](https://developercommunity.visualstudio.com/search?entry=suggestion&space=8&preview2=true&q=xamarin+android&stateGroup=active&ftype=idea&sort=votes) on Microsoft Developer Community.
  - File an issue in [GitHub Issues](https://github.com/xamarin/xamarin-android/issues/new/choose).
  - Discuss development and design on [Discord](https://aka.ms/dotnet-discord).

[![Discord](https://img.shields.io/badge/chat-on%20discord-brightgreen)](https://aka.ms/dotnet-discord)

# License

Copyright (c) .NET Foundation Contributors. All rights reserved.
Licensed under the [MIT](LICENSE) License.
