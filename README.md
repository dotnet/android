<img src="Documentation/images/banner.png" alt=".NET Android banner" height="145" >

.NET Android
===============

.NET Android provides open-source bindings of the Android SDK and tooling for use with
.NET managed languages such as C#. This ships as an optional [.NET workload][net-workload] for .NET 6+ that can 
be updated independently from .NET in order to respond to external dependency updates like new Android
platform and tooling.

While .NET Android is an essential part of [MAUI][maui-intro], it is still fully supported to be 
used independently for native Android development using .NET.

This repository is also home to the classic Xamarin.Android product.

[net-workload]: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-workload-install
[maui-intro]: https://learn.microsoft.com/en-us/dotnet/maui/what-is-maui

# Support

.NET Android is now part of .NET 6+ and follows the same support lifecycle as the [MAUI Support Lifecycle][maui-support-lifecycle].

Support for classic Xamarin.Android will end on **May 1, 2024** as per the [Xamarin Support Policy][xamarin-support-policy]:

> Xamarin support will end on May 1, 2024 for all classic Xamarin SDKs. Android 13 will be the final version classic Xamarin.Android will target.

[maui-support-lifecycle]: https://dotnet.microsoft.com/en-us/platform/support/policy/maui
[xamarin-support-policy]: https://dotnet.microsoft.com/en-us/platform/support/policy/xamarin

# Downloads

## Current

.NET Android ships as a workload through the `dotnet` workload system in [.NET 6+][dotnet-download]. See
the [workload documentation][workload-documentation] for installation commands.

[dotnet-download]: https://dotnet.microsoft.com/en-us/download
[workload-documentation]: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-workload-install

Classic Xamarin.Android installers are available here:

| Platform        | Link   |
|-----------------|--------|
| **Commercial Xamarin.Android 13.2.2 (d17-8)** for Windows+Visual Studio 2022                  | [Download][commercial-d17-8-Windows-x86_64] |
| **Commercial Xamarin.Android 13.2.2 (d17-8)** for VSMac 2022                                  | [Download][commercial-d17-8-macOS-x86_64]   |

[Previous Releases](Documentation/previous-releases.md) are also available for download.

[commercial-d17-8-Windows-x86_64]:        https://aka.ms/xamarin-android-commercial-d17-8-windows
[commercial-d17-8-macOS-x86_64]:          https://aka.ms/xamarin-android-commercial-d17-8-macos

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
