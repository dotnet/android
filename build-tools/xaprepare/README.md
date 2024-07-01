<!-- markdown-toc start - Don't edit this section. Run M-x markdown-toc-refresh-toc -->
**Table of Contents**

- [.NET for Android build preparation utility](#xamarinandroid-build-preparation-utility)
    - [Introduction](#introduction)
    - [Why?](#why)
    - [Supported operating systems](#supported-operating-systems)
    - [Prerequisites](#prerequisites)
        - [All systems](#all-systems)
        - [macOS and Linux](#macos-and-linux)
        - [macOS](#macos)
    - [Build configuration](#build-configuration)
        - [File naming convention](#file-naming-convention)
        - [Configuration directory](#configuration-directory)
    - [Running](#running)
        - [Scenarios](#scenarios)
        - [Invocation](#invocation)
        - [Log files](#log-files)

<!-- markdown-toc end -->
# .NET for Android build preparation utility

## Introduction

The task and purpose of this utility is to prepare the .NET for Android source tree for build by 
performing a number of steps which need to be done only once (or very few and far between) mostly after
the repository is freshly cloned.

The utility is written in C# as a .NET 4.7 console app and does not depend on any other code within the
`xamarin-android` repository.

## Why?

The utility replaces older system which was implemented using a vast collection of MSBuild projects, 
target files, property files distributed around the .NET for Android source tree which caused the following,
but not limited to, problems:

  - MSBuild doesn't very well interface with 3rd party build systems, which caused the need to employ a number
    of workarounds/hacks in order to make things work. The workarounds made the code confusing and not very
	friendly to occasional contributors.
  - Settings, configurations etc were often duplicated between makefiles and msbuild (and even between different
    MSBuild projects) which made it easy to make mistakes. It also made the whole setup hard to navigate.
  - Due to the way MSBuild works, we had to build some projects twice in order to get things working - once during
    preparation phase, second time during "normal" builds
  - Retrieval of settings/property values from makefiles used MSBuild to invoke its targets which then printed
    values to standard output to be captured by shell code inside the makefile. While it worked **most** of the time,
	it caused mysterious and hard to track errors when it did not work (e.g. the shell invoking msbuild to get some 
	value would interpret "garbage" output and produce nonsensical errors)
  
All of the above problems (and a few more) are addressed by this utility by putting all the configuration and code in
one place as well as the entire process using a real programming language which allows for more expressive and readable
implementation.

## Supported operating systems

Currently the utility supports `macOS`, ``Linux`` (``Debian`` and derivatives, `Arch` is to be implemented) 
with `Windows` support in the works.

## Prerequisites

The utility requires that the following is present on the host system:

### All systems
  - [NuGet](https://www.nuget.org/downloads)

### macOS and Linux
  - [Mono runtime](https://www.mono-project.com/download/stable/)

### macOS
  - [Homebrew](https://brew.sh/)
  - Xcode with command line utilities (for GNU Make as well as the compilers)

## Build configuration

The utility is designed to make it easy(-ier) to change all aspects of .NET for Android build preparation process including,
but not limited to:

 - Android API levels
 - Mono runtimes/compilers/cross compilers
 - Location of output files/logs/etc
 - Contents of various "bundles" created by the build
 - URLs to download Mono archive, installers etc from
 - Build system dependencies and required program versions
 
The idea here is to put all of the above, and more, in a single location (in a small number of C# source files) which contain
everyting that is required by other parts of the code, so that anyone (even with limited knowledge of the source tree or build
process) can make any necessary changes. The files are laid out in a way that the information found in them, albeit sometimes 
voluminous, is self-explanatory and should not require much effort to understand and modify.

### File naming convention

Some files have the operating system embeded before their `.cs` extension. Such files are built and used only when the preparation
utility is built on that particular operating system.

### Configuration directory

The files mentioned above are found in the [ConfigAndData](xaprepare/ConfigAndData) directory and are briefly described below.

 - [AbiNames.cs](xaprepare/ConfigAndData/AbiNames.cs)
   Rarely modified, contains all the target ABI names as used throughout the .NET for Android source as well as a number of
   helper methods used throughout the preparation utility code. **Be very careful** when modifying the names there as it may
   break the build!
 - [BuildAndroidPlatforms.cs](xaprepare/ConfigAndData/BuildAndroidPlatforms.cs)
   Modified whenever a new Android platform is added, this file names all of the Android API levels along with platform/API
   identifiers and .NET for Android framework names corresponding to specific API levels. The file also contains specification
   of minimum NDK API levels used for all the Android device targets.
 - [CommonLicenses.cs](xaprepare/ConfigAndData/CommonLicenses.cs)
   A file with constants containing paths to licenses commonly used by software .NET for Android uses. The licenses are used
   when generating Third Party Notices.
 - [Configurables](xaprepare/ConfigAndData/Configurables.cs)
   The file (and its OS-specific companions) contain all of the tunable bits and pieces of configuration that affect various
   aspects of the build. There are three subclasses with self-explanatory names: `Configurables.Paths`, `Configurables.Urls` and 
   `Configurables.Defaults`
 - [Runtimes.cs](xaprepare/ConfigAndData/Runtimes.cs)
   This file defines **all** of the Mono runtimes, BCL (Base Class Library) assemblies, utilities and components used by
   .NET for Android in the build as well as generated as part of the build for inclusion in bundles and installers.
 - [Dependencies/*.cs](xaprepare/ConfigAndData/Dependencies)
   Files in this directory contain dependencies (program and package names + versions) for all the supported operating systems.

## Running

### Scenarios

The utility employs the abstraction of "scenarios" which are collections of various steps to perform in order to achieve a
specific goal. The main scenario is named `Standard` and is the default one if no other scenario is named on the command line 
(by using the `-s NAME` parameter). You can list all the scenarios by issuing the following command from the root of .NET for Android
source tree:

```
make PREPARE_ARGS=--ls
```

### Invocation

In order to run the preparation utility on your development machine, all you need to do is to invoke the following command from
the root of the .NET for Android source tree:

```
make prepare
```

To get list of all command line parameters accepted by the utility, issue the following command:

```
make prepare-help
```

You can append the following parameters to the command line:

 - `PREPARE_ARGS=""`
   All and any command line parameters accepted by the utility
 - `PREPARE_SCENARIO=""`
   Name of the "scenario" to run
 - `PREPARE_AUTOPROVISION=0|1`
   If set to `0` (the default), the utility will take notice of missing/outdated software the build depends on and exit with an error
   should any such condition is detected. Setting the property to `1` will let the utility install the software (installation **may** 
   use `sudo` on Unix so you will need administrator/root credentials for it to work)
 - `PREPARE_AUTOPROVISION_SKIP_MONO=0|1`
   If set to `0` (the default), the utility will ensure the Mono MDK is installed.
   Setting the property to `1` will allow you to skip Mono MDK installation.
 - `V=1`
   Causes the run to output much more information (making output much more messy in the process) to the console. Normally this additional
   information is placed only in the log files generated by the utility.

### Log files

Log files are generated in the `bin/Build*/` directory (where the part indicated by asterisk here is either `Debug` or `Release` depending
on the chosen configuration) in files named using the following format: `prepare-TIMESTAMP.TAGS.log` where the components in capital letters
have the following format:

  - `TIMESTAMP`
    yyyymmddThhmmss (``yyyy`` - year, `mm` - month, `dd` - day, `hh` - hour, `mm` - minute, `ss` - second)
  - `TAGS`
    Arbitrary set of strings as set by various steps/tasks, usually self-explanatory
