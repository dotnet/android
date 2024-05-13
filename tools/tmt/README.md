<!-- markdown-toc start - Don't edit this section. Run M-x markdown-toc-refresh-toc -->
**Table of Contents**

- [Introduction](#introduction)
    - [Reporting](#reporting)
    - [Filtering](#filtering)
        - [Type name filtering](#type-name-filtering)
        - [Architecture filtering](#architecture-filtering)
- [Output format](#output-format)
    - [Debug maps](#debug-maps)
        - [Java to Managed](#java-to-managed)
        - [Managed to Java](#managed-to-java)
    - [Release maps](#release-maps)
        - [Raw output](#raw-output)
            - [Java to Managed](#java-to-managed-1)
            - [Managed to Java](#managed-to-java-1)
        - [Processed output](#processed-output)
            - [Java to Managed](#java-to-managed-2)
            - [Managed to Java](#managed-to-java-2)
- [Command line options](#command-line-options)
    - [Sample invocations](#sample-invocations)
        - [Filter by type name](#filter-by-type-name)
        - [Dump all typemaps in a project directory](#dump-all-typemaps-in-a-project-directory)
        - [Dump all type maps from an apk](#dump-all-type-maps-from-an-apk)
        - [Dump only x86_64 typemap from an apk](#dump-only-x86_64-typemap-from-an-apk)
        - [Dump typemap from a single shared library](#dump-typemap-from-a-single-shared-library)

<!-- markdown-toc end -->

# Introduction

This is a utility program to read .NET for Android application type
maps compiled into a shared library named `libxamarin-app.so`.  This
library is compiled for each native architecture separately and
packaged together with the application into the APK archive.

Type maps are used on run time to map Managed types to Java types and
vice versa.  This process differs between `Release` and `Debug`
builds, but in both cases the maps are stored as binary data in the
native shared library.  This fact makes it hard to determine which
Managed/Java type maps to its counterpart (especially in the `Release`
mode where Managed type names aren't stored at all - the types are
instead identified by their assembly module's GUID and the type token ID).

This utility can read both `Release` and `Debug` maps and is able to
find `libxamarin-app.so` when pointed to one of the following
locations:

 * Application APK/AAB archives
 * Application project top directory
   * `obj` subdirectory
   * `obj/$CONFIGURATION` subdirectory
 * Any directory which contains `libxamarin-app.so`
 * Path to a concrete `libxamarin-app.so` file

If the utility reads `Release` map from a `libxamarin-app.so` in a
directory that is not one of the above locations, it will not be able
to obtain Managed type and assembly names.

When passed a path to APK/AAB archive, the utility will by default
load all the `libxamarin-app.so` shared libraries found in the
archive - one for each enabled target architecture.  This can be
disabled by using the `-a` option (see [Filtering](#architecture-filtering) below)

The only required parameter is a path a location containing the type
maps, as defined above.

Optionally, the location path may be followed by a single parameter
specifying a .NET regular expression to match Managed and Java types
in order to filter the map (see [Filtering](#type-name-filtering) below)

## Reporting 

By default, the utility generates files with full contents of type
maps read from the shared library.  The output differs depending on
whether the `Release` or `Debug` configuration is found in the shared
library.

For `Release`, two files for each native shared library are generated:

  * Raw output (.raw)
    Contains uninterpreted raw data from the map.  This is the most
    technical, detailed format.  It performs no type name lookup,
    contains information about managed assemblies, their modules,
    identifiers etc.
  * Processed output (.txt)
    Contains a comma-separated value collection of type mappings, with
    most of the technical details of the raw format removed, managed
    type names looked up (if possible).  There are two modes for this
    format - full and abbreviated.  They differ in that the latter
    does not contain information about Managed type module MVID or its
    token ID.

For `Debug`, only one file is generated with the same information as
the `Processed output` format above.

In both cases, file names are constructed following the pattern below:

    typemap-vX-CONFIGURATION-ARCHITECTURE-KIND.{raw,txt}

Where

  * `X` stands for the type map format version
  * `CONFIGURATION` is either `Debug` or `Release`
  * `ARCHITECTURE` is one of: `ARM`, `ARM64`, `X86`, `X86_64`
  * `KIND` is either `managed` or `java`

If regular expression type name filtering is not enabled, the utility
will not print the map contents to the console.  This is because even
small applications can contain thousands of entries and the whole map
content is better written to a file instead of producing voluminous
output on the screen.

## Filtering

### Type name filtering

Passing a regular expression to the utility causes it to match type
names (both Managed and Java for **each** loaded entry, regardless on
whether the current map being processed is java-to-managed or
managed-to-java) against it and report the matches to the console.

Also, in this case the files are not generated by the default, which
can be changed by using the `-g` option.  This is because filtering
mode is mostly useful for "quick look" at the maps and the full output
will likely be of no interest in this instance.

### Architecture filtering

By default the utility reads `libxamarin-app.so` for any architecture
it finds in the location indicated on command line.  The `-a` option
can be used to limit the set of architectures by specifying a
comma-separated list of architecture names from the following set
(architecture names are case-insensitive): `ARM`, `ARM64`, `X86` and
`X86_64`

# Output format

## Debug maps

### Java to Managed
```
Java-Type-Name  Managed-Type-Name       Is-Duplicate-Type-Entry?
android/accessibilityservice/AccessibilityButtonController      Android.AccessibilityServices.AccessibilityButtonController, Mono.Android       
android/accessibilityservice/AccessibilityButtonController$AccessibilityButtonCallback  Android.AccessibilityServices.AccessibilityButtonController+AccessibilityButtonCallback, Mono.Android   duplicate entry
android/accessibilityservice/AccessibilityGestureEvent  Android.AccessibilityServices.AccessibilityGestureEvent, Mono.Android
```

### Managed to Java

```
Managed-Type-Name       Java-Type-Name  Is-Duplicate-Type-Entry?
Android.AccessibilityServices.AccessibilityButtonController, Mono.Android       android/accessibilityservice/AccessibilityButtonController      
Android.AccessibilityServices.AccessibilityButtonController+AccessibilityButtonCallback, Mono.Android   android/accessibilityservice/AccessibilityButtonController$AccessibilityButtonCallback  
Android.AccessibilityServices.AccessibilityButtonController+AccessibilityButtonCallbackInvoker, Mono.Android    android/accessibilityservice/AccessibilityButtonController$AccessibilityButtonCallback
```

## Release maps

### Raw output

Raw output dumps the typemap data without interpretation - it doesn't
perform `MVID+TokenID` translation into fully qualified type name.

#### Java to Managed

```
MANAGED_MODULE_INDEX    TYPE_TOKEN_DECIMAL (TYPE_TOKEN_HEXADECIMAL)     JAVA_TYPE_NAME
        1       33554623 (020000BF)     android/app/Activity
        1       33554624 (020000C0)     android/app/Application
        1       33554628 (020000C4)     android/app/backup/BackupAgent
        1       33554630 (020000C6)     android/app/backup/BackupDataInput
```

#### Managed to Java

```
TYPE_TOKEN_DECIMAL (TYPE_TOKEN_HEXADECIMAL)     JAVA_MAP_INDEX

Module 0000: HelloLibrary (MVID: 863f5810-e772-4a82-ab83-eb8530dc5d8c; entries: 2; duplicates: 0)
        map:
                33554434 (02000002)     157
                33554435 (02000003)     71
        no duplicates

Module 0001: Mono.Android (MVID: ec958fab-bf1d-48b4-b93b-ea43dd50f503; entries: 155; duplicates: 25)
        map:
                33554508 (0200004C)     147
                33554510 (0200004E)     149
```

### Processed output

Dumps the typemap after translating the raw information above into
human-readable fully qualified type names.

#### Java to Managed
```
Java-Type-Name  Managed-Type-Name       Is-Generic-Type?        MVID    Token-ID
android/app/Activity    Android.App.Activity, Mono.Android              ec958fab-bf1d-48b4-b93b-ea43dd50f503    33554623 (0x020000BF)
android/app/Application Android.App.Application, Mono.Android           ec958fab-bf1d-48b4-b93b-ea43dd50f503    33554624 (0x020000C0)
android/app/backup/BackupAgent  Android.App.Backup.BackupAgent, Mono.Android            ec958fab-bf1d-48b4-b93b-ea43dd50f503    33554628 (0x020000C4)
mono/android/runtime/JavaArray  [name unknown], Mono.Android    generic, ignored        ec958fab-bf1d-48b4-b93b-ea43dd50f503    0 (0x00000000)
```

The last entry above is for a generic Managed type, which typemap
marks with an invalid token ID of `0` and, thus, its name cannot be
looked up and in its place `[name unknown]` is printed.  The entry
still serves a purpose by showing the associated Java type, the
assembly and module UUID (MVID).

#### Managed to Java

```
Managed-Type-Name       Java-Type-Name  Is-Generic-Type?        Is-Duplicate-Type-Entry?        MVID    Token-ID
Mono.Samples.Hello.LibraryActivity, HelloLibrary        mono/samples/hello/LibraryActivity                                      863f5810-e772-4a82-ab83-eb8530dc5d8c    33554434 (0x02000002)
Mono.Samples.Hello.MyBackupAgent, HelloLibrary  crc64fb76f5491eb54ddd/MyBackupAgent                                     863f5810-e772-4a82-ab83-eb8530dc5d8c    33554435 (0x02000003)
HelloWorld.MainActivity, HelloWorld     example/MainActivity                                    8f7ca9c7-fa38-4784-b1de-d110106a1734    33554434 (0x02000002)
Android.App.Activity, Mono.Android      android/app/Activity                                    ec958fab-bf1d-48b4-b93b-ea43dd50f503    33554623 (0x020000BF)
```

# Command line options

```
Usage: tmt [OPTIONS] <FILE.apk|FILE.aab|libxamarin-app.so|PROJECT_DIR> [FILTER_REGEX]

OPTIONS are:

  -j, --only-java            Process only the java-to-managed map
  -m, --only-managed         Process only the managed-to-java map
  -s, --short-report         Omit some map details from the report (e.g. MVID
                               and TokenID from managed-to-java map)
  -o, --output-directory=DIR Write the report files in the DIR directory
                               instead of the current one
  -a, --arch=ARCH_LIST       Limit reporting only to the specified
                               architectures. ARCH_LIST is a comma-separated
                               list of architectures (one of, case-insensitive:
                               ARM, ARM64, X86, X86_64)
  -1, --only-first           Process only the first shared library from the APK/
                               AAB archive or a project directory. Architecture
                               filter is ignored in this case. This is the
                               default action when regex filtering is used,
                               otherwise it defaults to `false`
  -g, --generate-files       Generate report files. If regex filtering is used,
                               this setting defaults to `false`, it is `true`
                               otherwise.

  -v, --verbose              Show debug messages
```

## Sample invocations

### Filter by type name

In this mode no files are produced, the matched entries are printed to
the console

```shell
$ bin/Debug/bin/tmt samples/HelloWorld '.*Java.Lang.Object'
samples/HelloWorld/obj/Release/app_shared_libraries/arm64-v8a/libxamarin-app.so:
  File Type: Xamarin App Release DSO
  Format version: 1
  Map kind: Release
  Map architecture: ARM64
  Managed to Java entries: 183
  Java to Managed entries: 158 (without duplicates)

  Matching entries (Java to Managed):
    java/lang/Object -> Java.Lang.Object, Mono.Android; MVID: ec958fab-bf1d-48b4-b93b-ea43dd50f503; Token ID: 33554768 (0x02000150)

  Matching entries (Managed to Java):
    Java.Lang.Object; MVID: ec958fab-bf1d-48b4-b93b-ea43dd50f503; Token ID: 33554768 (0x02000150) -> java/lang/Object
```

### Dump all typemaps in a project directory
```shell
# 
# Alternative invocations with the same result:
#
# bin/Debug/bin/tmt/tmt samples/HelloWorld/obj
# bin/Debug/bin/tmt/tmt samples/HelloWorld/obj/Debug
#
$ bin/Debug/bin/tmt samples/HelloWorld/
samples/HelloWorld/obj/Debug/app_shared_libraries/arm64-v8a/libxamarin-app.so:
  Loading Java to Managed map: 6062 entries, please wait...
  Loading Managed to Java map: 6062 entries, please wait...
  File Type: Xamarin App Debug DSO
  Format version: 1
  Map kind: Debug
  Map architecture: ARM64
  Managed to Java entries: 6062
  Java to Managed entries: 5376 (without duplicates)
  Java to Managed output: typemap-v1-Debug-ARM64-java.txt
  Managed to Java output: typemap-v1-Debug-ARM64-managed.txt

samples/HelloWorld/obj/Debug/app_shared_libraries/armeabi-v7a/libxamarin-app.so:
  Loading Java to Managed map: 6062 entries, please wait...
  Loading Managed to Java map: 6062 entries, please wait...
  File Type: Xamarin App Debug DSO
  Format version: 1
  Map kind: Debug
  Map architecture: ARM
  Managed to Java entries: 6062
  Java to Managed entries: 5376 (without duplicates)
  Java to Managed output: typemap-v1-Debug-ARM-java.txt
  Managed to Java output: typemap-v1-Debug-ARM-managed.txt

samples/HelloWorld/obj/Debug/app_shared_libraries/x86/libxamarin-app.so:
  Loading Java to Managed map: 6062 entries, please wait...
  Loading Managed to Java map: 6062 entries, please wait...
  File Type: Xamarin App Debug DSO
  Format version: 1
  Map kind: Debug
  Map architecture: X86
  Managed to Java entries: 6062
  Java to Managed entries: 5376 (without duplicates)
  Java to Managed output: typemap-v1-Debug-X86-java.txt
  Managed to Java output: typemap-v1-Debug-X86-managed.txt

samples/HelloWorld/obj/Debug/app_shared_libraries/x86_64/libxamarin-app.so:
  Loading Java to Managed map: 6062 entries, please wait...
  Loading Managed to Java map: 6062 entries, please wait...
  File Type: Xamarin App Debug DSO
  Format version: 1
  Map kind: Debug
  Map architecture: X86_64
  Managed to Java entries: 6062
  Java to Managed entries: 5376 (without duplicates)
  Java to Managed output: typemap-v1-Debug-X86_64-java.txt
  Managed to Java output: typemap-v1-Debug-X86_64-managed.txt
```

### Dump all type maps from an apk

```shell
$ bin/Debug/bin/tmt samples/HelloWorld/bin/Debug/com.xamarin.android.helloworld-Signed.apk 
samples/HelloWorld/bin/Debug/com.xamarin.android.helloworld-Signed.apk!lib/arm64-v8a/libxamarin-app.so:
  Loading Java to Managed map: 6062 entries, please wait...
  Loading Managed to Java map: 6062 entries, please wait...
  File Type: Xamarin App Debug DSO
  Format version: 1
  Map kind: Debug
  Map architecture: ARM64
  Managed to Java entries: 6062
  Java to Managed entries: 5376 (without duplicates)
  Java to Managed output: typemap-v1-Debug-ARM64-java.txt
  Managed to Java output: typemap-v1-Debug-ARM64-managed.txt

samples/HelloWorld/bin/Debug/com.xamarin.android.helloworld-Signed.apk!lib/armeabi-v7a/libxamarin-app.so:
  Loading Java to Managed map: 6062 entries, please wait...
  Loading Managed to Java map: 6062 entries, please wait...
  File Type: Xamarin App Debug DSO
  Format version: 1
  Map kind: Debug
  Map architecture: ARM
  Managed to Java entries: 6062
  Java to Managed entries: 5376 (without duplicates)
  Java to Managed output: typemap-v1-Debug-ARM-java.txt
  Managed to Java output: typemap-v1-Debug-ARM-managed.txt

samples/HelloWorld/bin/Debug/com.xamarin.android.helloworld-Signed.apk!lib/x86/libxamarin-app.so:
  Loading Java to Managed map: 6062 entries, please wait...
  Loading Managed to Java map: 6062 entries, please wait...
  File Type: Xamarin App Debug DSO
  Format version: 1
  Map kind: Debug
  Map architecture: X86
  Managed to Java entries: 6062
  Java to Managed entries: 5376 (without duplicates)
  Java to Managed output: typemap-v1-Debug-X86-java.txt
  Managed to Java output: typemap-v1-Debug-X86-managed.txt

samples/HelloWorld/bin/Debug/com.xamarin.android.helloworld-Signed.apk!lib/x86_64/libxamarin-app.so:
  Loading Java to Managed map: 6062 entries, please wait...
  Loading Managed to Java map: 6062 entries, please wait...
  File Type: Xamarin App Debug DSO
  Format version: 1
  Map kind: Debug
  Map architecture: X86_64
  Managed to Java entries: 6062
  Java to Managed entries: 5376 (without duplicates)
  Java to Managed output: typemap-v1-Debug-X86_64-java.txt
  Managed to Java output: typemap-v1-Debug-X86_64-managed.txt
```

### Dump only x86_64 typemap from an apk

```shell
$ bin/Debug/bin/tmt -a x86_64 samples/HelloWorld/bin/Debug/com.xamarin.android.helloworld-Signed.apk 
samples/HelloWorld/bin/Debug/com.xamarin.android.helloworld-Signed.apk!lib/x86_64/libxamarin-app.so:
  Loading Java to Managed map: 6062 entries, please wait...
  Loading Managed to Java map: 6062 entries, please wait...
  File Type: Xamarin App Debug DSO
  Format version: 1
  Map kind: Debug
  Map architecture: X86_64
  Managed to Java entries: 6062
  Java to Managed entries: 5376 (without duplicates)
  Java to Managed output: typemap-v1-Debug-X86_64-java.txt
  Managed to Java output: typemap-v1-Debug-X86_64-managed.txt
```

### Dump typemap from a single shared library

```shell
$ bin/Debug/bin/tmt samples/HelloWorld/obj/Debug/app_shared_libraries/arm64-v8a/libxamarin-app.so 
samples/HelloWorld/obj/Debug/app_shared_libraries/arm64-v8a/libxamarin-app.so:
  Loading Java to Managed map: 6062 entries, please wait...
  Loading Managed to Java map: 6062 entries, please wait...
  File Type: Xamarin App Debug DSO
  Format version: 1
  Map kind: Debug
  Map architecture: ARM64
  Managed to Java entries: 6062
  Java to Managed entries: 5376 (without duplicates)
  Java to Managed output: typemap-v1-Debug-ARM64-java.txt
  Managed to Java output: typemap-v1-Debug-ARM64-managed.txt
```
