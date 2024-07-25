<img src="Documentation/images/banner.png" alt=".NET for Android banner" height="145" >

.NET for Android
===============

.NET for Android provides open-source bindings of the Android SDK and tooling for use with
.NET managed languages such as C#. This ships as an optional [.NET workload][net-workload] for .NET 6+ that can 
be updated independently from .NET in order to respond to external dependency updates like new Android
platform and tooling.

.NET for Android is part of [.NET MAUI][maui-intro], and may also be used independently for native Android development using .NET.

[net-workload]: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-workload-install
[maui-intro]: https://learn.microsoft.com/en-us/dotnet/maui/what-is-maui

# Support

.NET for Android is part of .NET MAUI, since it was introduced in May 2022 as part of .NET 6, and is currently supported as described in the [.NET MAUI Support Policy][maui-support-policy].

Support for Xamarin.Android ended on **May 1, 2024** as per the [Xamarin Support Policy][xamarin-support-policy]:

> Xamarin support ended on May 1, 2024 for all Xamarin SDKs including Xamarin.Forms. Android API 34 and Xcode 15 SDKs (iOS and iPadOS 17, macOS 14) are the final versions Xamarin targets from existing Xamarin SDKs (i.e. no new APIs are planned).

Follow the [official upgrade guidance](https://learn.microsoft.com/dotnet/maui/migration) to bring your Xamarin applications to the latest version of .NET.

[maui-support-policy]: https://dotnet.microsoft.com/en-us/platform/support/policy/maui
[xamarin-support-policy]: https://dotnet.microsoft.com/en-us/platform/support/policy/xamarin

# Downloads

.NET for Android ships as a workload through the `dotnet` workload system in [.NET 6+][dotnet-download]. 

In its simplest form, .NET for Android can be installed by running:

```
dotnet workload install android
```

See the [.NET workload documentation][workload-documentation] for additional installation commands and options.

[dotnet-download]: https://dotnet.microsoft.com/en-us/download
[workload-documentation]: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-workload-install

While no longer supported, Classic Xamarin.Android installers are still available [here](Documentation/previous-releases.md).

# Contributing

If you are interested in fixing issues and contributing directly to the code base, please see the following:

  - [How to build and run from source](Documentation/README.md#building-from-source)
  - [The development workflow, and using your build](Documentation/README.md#development-workflow)
  - [Coding Guidelines](http://www.mono-project.com/community/contributing/coding-guidelines/)
  - [Submitting pull requests](https://github.com/xamarin/xamarin-android/wiki/Submitting-Bugs,-Feature-Requests,-and-Pull-Requests#pull-requests)

# Feedback

  - Ask a question on [Stack Overflow](https://stackoverflow.com/questions/tagged/xamarin.android) or [Microsoft Q&A](https://docs.microsoft.com/en-us/answers/topics/dotnet-android.html).
  - [Request a new feature or vote for popular feature requests](https://developercommunity.visualstudio.com/search?entry=suggestion&space=8&preview2=true&q=xamarin+android&stateGroup=active&ftype=idea&sort=votes) on Microsoft Developer Community.
  - File an issue in [GitHub Issues](https://github.com/xamarin/xamarin-android/issues/new/choose).
  - Discuss development and design on [Discord](https://aka.ms/dotnet-discord).

[![Discord](https://img.shields.io/badge/chat-on%20discord-brightgreen)](https://aka.ms/dotnet-discord)

# License

Copyright (c) .NET Foundation Contributors. All rights reserved.
Licensed under the [MIT](LICENSE) License.
