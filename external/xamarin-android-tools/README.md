# xamarin-android-tools

**xamarin-android-tools** is a repo to easily share code between the
[xamarin-android][x-a] repo and the Xamarin.Android commercial tooling,
such as IDE extensions, without requiring that the IDE extensions
submodule the entire **xamarin-android** repo, which is gigantic.

[x-a]: https://github.com/xamarin/xamarin-android

# Build Requirements

**xamarin-android-tools** requires Mono 4.8 or later and `nuget`.

# Build Configuration

The default `make all` target accepts the following optional
**make**(1) variables:

  * `$(CONFIGURATION)`: The configuration to build.
    Possible values include `Debug` and `Release`.
    The default value is `Debug`.
  * `$(V)`: Controls build verbosity. When set to a non-zero value,
    The build is built with `/v:diag` logging.

# Build

To build **xamarin-android-tools**, first prepare the project:

	make prepare

This will perform `nuget restore` and any other pre-build tasks
that need to be performed.

Next, run `make`:

	make

# Tests

To run the unit tests:

	make run-all-tests

# Build Output Directory Structure

There are two configurations, `Debug` and `Release`, controlled by the
`$(Configuration)` MSBuild property or the `$(CONFIGURATION)` make variable.

The `bin\$(Configuration)` directory, e.g. `bin\Debug`, contains
*redistributable* artifacts. The `bin\Test$(Configuration)` directory,
e.g. `bin\TestDebug`, contains unit tests and related files.

* `bin\$(Configuration)`: redistributable build artifacts.
* `bin\Test$(Configuration)`: Unit tests and related files.

# Contributing

This project has adopted the code of conduct defined by the Contributor Covenant
to clarify expected behavior in our community. For more information, see the
[.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

# Mailing Lists

To discuss this project, and participate in the design, we use the
[android-devel@lists.xamarin.com](http://lists.xamarin.com/mailman/listinfo/android-devel) mailing list.

# Coding Guidelines

We use [Mono's Coding Guidelines](http://www.mono-project.com/community/contributing/coding-guidelines/).

# Reporting Bugs

We use [GitHub](https://github.com/xamarin/xamarin-android-tools/issues) to track issues.
