This is the D8 and R8 integration specification for .NET for Android.

# What is D8? What is R8?

At a high level, here are the steps that occur during an Android
application's Java compilation:
- `javac` compiles Java code
- `desugar` remove's the "sugar" (from Java 8 features) that are not
  fully supported on Android
- ProGuard shrinks compiled Java code
- `dx` "dexes" compiled Java code into Android [dex][dex] format. This
  is an alternate Java bytecode format supported by the Android
  platform.

This process has a few issues, such as:
- [proguard](https://www.guardsquare.com/en/products/proguard/manual)
  is made by a third party, and aimed for Java in general (not Android
  specific)
- `dx` is slower than it _could_ be

So in 2017, Google announced a "next-generation" dex compiler named
[D8](https://android-developers.googleblog.com/2017/08/next-generation-dex-compiler-now-in.html).

- D8 is a direct replacement for `dx`
- R8 is a replacement for ProGuard, that also "dexes" at the same
  time. If using R8, a D8 call is not needed.

Both tools have support for various other Android-specifics:
- Both `desugar` by default unless the `--no-desugaring` switch is
  specified
- Both support [multidex][multidex], although `d8` does not have
  support for using the ProGuard rules format (the
  `--main-dex-rules` switch).
- R8 has full support for [multidex][multidex].

Additionally, R8 is geared to be backwards compatible to ProGuard.
It uses the same file format for configuration and command-line
parameters as ProGuard.  However, at the time of writing this, there
are still several flags/features not implemented in R8 yet.

For more information on how R8 compares to ProGuard, please see
[this comparison from the ProGuard team](https://www.guardsquare.com/en/blog/proguard-and-r8).

You can find the source for D8 and R8 at:
<https://r8.googlesource.com/r8/>

For reference, `d8 --help`:
```
Usage: d8 [options] <input-files>
 where <input-files> are any combination of dex, class, zip, jar, or apk files
 and options are:
  --debug                 # Compile with debugging information (default).
  --release               # Compile without debugging information.
  --output <file>         # Output result in <outfile>.
                          # <file> must be an existing directory or a zip file.
  --lib <file>            # Add <file> as a library resource.
  --classpath <file>      # Add <file> as a classpath resource.
  --min-api               # Minimum Android API level compatibility
  --intermediate          # Compile an intermediate result intended for later
                          # merging.
  --file-per-class        # Produce a separate dex file per input class
  --no-desugaring         # Force disable desugaring.
  --main-dex-list <file>  # List of classes to place in the primary dex file.
  --version               # Print the version of d8.
  --help                  # Print this message.
```

For reference, `r8 --help`:
```
Usage: r8 [options] <input-files>
 where <input-files> are any combination of dex, class, zip, jar, or apk files
 and options are:
  --release                # Compile without debugging information (default).
  --debug                  # Compile with debugging information.
  --output <file>          # Output result in <file>.
                           # <file> must be an existing directory or a zip file.
  --lib <file>             # Add <file> as a library resource.
  --min-api                # Minimum Android API level compatibility.
  --pg-conf <file>         # Proguard configuration <file>.
  --pg-map-output <file>   # Output the resulting name and line mapping to <file>.
  --no-tree-shaking        # Force disable tree shaking of unreachable classes.
  --no-minification        # Force disable minification of names.
  --no-desugaring          # Force disable desugaring.
  --main-dex-rules <file>  # Proguard keep rules for classes to place in the
                           # primary dex file.
  --main-dex-list <file>   # List of classes to place in the primary dex file.
  --main-dex-list-output <file>  # Output the full main-dex list in <file>.
  --version                # Print the version of r8.
  --help                   # Print this message.
```

# What did .NET for Android do *before* D8/R8?

In other words, what is currently happening *before* we introduce D8/R8 support?

1. The [Javac](https://github.com/xamarin/xamarin-android/blob/221a2190ebb3aaec9ecd9b1cf8f7f6174c43153a/src/Xamarin.Android.Build.Tasks/Tasks/Javac.cs)
   MSBuild task compiles `*.java` files to a `classes.zip` file.
2. The [Desugar](https://github.com/xamarin/xamarin-android/blob/221a2190ebb3aaec9ecd9b1cf8f7f6174c43153a/src/Xamarin.Android.Build.Tasks/Tasks/Desugar.cs)
   MSBuild task "desugars" using `desugar_deploy.jar` if
   `$(AndroidEnableDesugar)` is `True`.
3. The [Proguard](https://github.com/xamarin/xamarin-android/blob/221a2190ebb3aaec9ecd9b1cf8f7f6174c43153a/src/Xamarin.Android.Build.Tasks/Tasks/Proguard.cs)
   MSBuild task shrinks the compiled Java code if
   `$(AndroidEnableProguard)` is `True`. Developers may also supply
   custom proguard configuration files via `ProguardConfiguration`
   build items.
4. The [CreateMultiDexMainDexClassList](https://github.com/xamarin/xamarin-android/blob/221a2190ebb3aaec9ecd9b1cf8f7f6174c43153a/src/Xamarin.Android.Build.Tasks/Tasks/CreateMultiDexMainDexClassList.cs)
   MSBuild task runs `proguard` to generate a final, combined
   `multidex.keep` file if `$(AndroidEnableMultiDex)` is `True`.
   Developers can also supply custom `multidex.keep` files via
   `MultiDexMainDexList` build items.
5. The [CompileToDalvik](https://github.com/xamarin/xamarin-android/blob/221a2190ebb3aaec9ecd9b1cf8f7f6174c43153a/src/Xamarin.Android.Build.Tasks/Tasks/CompileToDalvik.cs)
   MSBuild task runs `dx.jar` to generate a final `classes.dex` file
   in `$(IntermediateOutputPath)android\bin`. If `multidex` is
   enabled, a `classes2.dex` (and potentially more) are also generated
   in this location.

# What does this process look like with D8 / R8 enabled?

.NET for Android now has two new MSBuild tasks: `<R8/>` and `<D8/>`.

1. The [Javac](https://github.com/xamarin/xamarin-android/blob/221a2190ebb3aaec9ecd9b1cf8f7f6174c43153a/src/Xamarin.Android.Build.Tasks/Tasks/Javac.cs)
   MSBuild task will remain unchanged.
2. `D8` will run if `$(AndroidEnableMultiDex)` is `False`,
   `$(AndroidLinkTool)` is not `r8`, and "desugar" by default.
3. Otherwise, `R8` will run if `$(AndroidEnableMultiDex)` is `True` or
   `$(AndroidLinkTool)` is `r8` and will also "desugar" by default.

So in addition to be being faster in general (if Google's claims are
true), we will be calling a *single* command line tool to produce dex
files!

# So how do developers use it? What are sensible MSBuild property defaults?

Currently, a `csproj` file might have the following properties:
```xml
<Project>
    <PropertyGroup>
        <AndroidEnableProguard>True</AndroidEnableProguard>
        <AndroidEnableMultiDex>True</AndroidEnableMultiDex>
        <AndroidEnableDesugar>True</AndroidEnableDesugar>
    </PropertyGroup>
</Project>
```

To enable the new behavior, we have introduced two new enum-style
properties:
- `$(AndroidDexTool)` - supports `dx` or `d8`
- `$(AndroidLinkTool)` - supports `proguard` or `r8`

But for an existing project, a developer could opt-in to the new
behavior with two properties:
```xml
<Project>
    <PropertyGroup>
        <AndroidEnableProguard>True</AndroidEnableProguard>
        <AndroidEnableMultiDex>True</AndroidEnableMultiDex>
        <AndroidEnableDesugar>True</AndroidEnableDesugar>
        <!--New properties-->
        <AndroidDexTool>d8</AndroidDexTool>
        <AndroidLinkTool>r8</AndroidLinkTool>
    </PropertyGroup>
</Project>
```

There should be two new MSBuild properties to configure here, because:
- You could use `D8` in combination with `proguard`, as `R8` is not
  "feature complete" in comparison to `proguard`.
- You may not want to use code shrinking at all, but still use `D8`
  instead of `dx`.
- You shouldn't be able to use `dx` in combination with `R8`, it
  doesn't make sense.
- Developers should be able to use the existing properties for
  enabling code shrinking, `multidex`, and `desugar`.

Our reasonable defaults would be:
- If `AndroidDexTool` is omitted, `dx` and `CompileToDalvik`
  should be used. Until D8/R8 integration is deemed stable and enabled
  by default.
- If `AndroidDexTool` is `d8` and `AndroidEnableDesugar` is
  omitted, `AndroidEnableDesugar` should be enabled.
- If `AndroidLinkTool` is omitted and `AndroidEnableProguard` is
  `true`, we should default `AndroidLinkTool` to `proguard`.

MSBuild properties default to:
```xml
<AndroidDexTool Condition=" '$(AndroidDexTool)' == '' ">dx</AndroidDexTool>
<!--NOTE: $(AndroidLinkTool) would be blank if code shrinking is not used at all-->
<AndroidLinkTool Condition=" '$(AndroidLinkTool)' == '' And '$(AndroidEnableProguard)' == 'True' ">proguard</AndroidLinkTool>
<AndroidEnableDesugar Condition=" '$(AndroidEnableDesugar)' == '' And ('$(AndroidDexTool)' == 'd8' Or '$(AndroidLinkTool)' == 'r8') ">True</AndroidEnableDesugar>
```

If a user specifies combinations of properties:
- `AndroidDexTool` = `d8` and `AndroidEnableProguard` = `True`
  - `AndroidLinkTool` will get set to `proguard`
- `AndroidDexTool` = `dx` and `AndroidLinkTool` = `r8`
  - This combination doesn't really *make sense*, but we don't need to
    do anything: only `R8` will be called because it dexes and shrinks
    at the same time.
- `AndroidEnableDesugar` is enabled when omitted, if either `d8` or
  `r8` are used

For new projects that want to use D8/R8, code shrinking, and
`multidex`, it would make sense to specify:
```xml
<Project>
    <PropertyGroup>
        <AndroidEnableMultiDex>True</AndroidEnableMultiDex>
        <AndroidDexTool>d8</AndroidDexTool>
        <AndroidLinkTool>r8</AndroidLinkTool>
    </PropertyGroup>
</Project>
```

# Additional D8 / R8 settings?

`--debug` or `--release` needs to be explicitly specified for both D8
and R8. We use the [AndroidIncludeDebugSymbols][debug_symbols]
property for this.

`$(AndroidD8ExtraArguments)` and `$(AndroidR8ExtraArguments)` can be
used to explicitly pass additional flags to D8 and R8.

# How are we compiling / shipping D8 and R8?

We have added a submodule to `xamarin-android` for
[r8](https://r8.googlesource.com/r8/). It will be pinned to a commit
with a reasonable release tag, such as `1.2.50` for now.

To build r8, we have to:
- Download and unzip a tool named [depot_tools][depot_tools] from the
  Chromium project
- Put the path to `depot_tools` in `$PATH`
- Run `gclient` so it will download/bootstrap gradle, python, and
  other tools
- Run `python tools\gradle.py d8 r8` to compile `d8.jar` and `r8.jar`
- We will need to ship `d8.jar` and `r8.jar` in our installers,
  similar to how we are shipping `desugar_deploy.jar`

# Performance Comparison

| MSBuild Target         | Options Enabled        | Time    | APK size (bytes) | dex size (bytes) |
| ---                    | ---                    | ---:    | ---:             | ---:             |
| _CompileToDalvikWithDx | n/a                    | 11074ms | 13378157         | 3894720          |
| _CompileToDalvikWithD8 | d8, (desugar enabled)  | 8543ms  | 13124205         | 3314064          |
| _CompileToDalvikWithD8 | d8, (desugar disabled) | 9550ms  | 13124205         | 3314064          |
| _CompileToDalvikWithDx | multi-dex              | 15632ms | 13390498         | 3916496          |
| _CompileToDalvikWithD8 | d8, multi-dex          | 25979ms | 13054626         | 3264096          |
| _CompileToDalvikWithDx | proguard               | 11903ms | 12804717         | 2446964          |
| _CompileToDalvikWithD8 | d8, r8                 | 13799ms | 12513901         | 1835588          |
| _CompileToDalvikWithDx | multi-dex, proguard    | 17279ms | 12804770         | 2449512          |
| _CompileToDalvikWithD8 | d8, multi-dex, r8      | 13792ms | 12513954         | 1837588          |

_NOTE: desugar is enabled by default with d8/r8_

I timed this builds with [this script][powershell_script], with a "Hello World" Xamarin.Forms app. Build logs here: [d8andr8.zip][d8andr8_zip]

One can draw their own conclusions on which options are faster, better, smaller.

Some of my thoughts:
- Default options for d8 and r8 seem to be faster?
- Disabling `desugar` is slower?
- Enabling `multi-dex` makes the dex file larger, because new classes are required. The app wasn't large enough to warrant a `classes2.dex`.
- `d8` does not support multi-dex, and so choosing `d8` + `multi-dex` actually runs `r8` with `--no-tree-shaking --no-minification`. These options are _slower_?

[dex]: https://source.android.com/devices/tech/dalvik/dalvik-bytecode
[multidex]: https://developer.android.com/studio/build/multidex
[debug_symbols]: https://github.com/xamarin/xamarin-android/blob/221a2190ebb3aaec9ecd9b1cf8f7f6174c43153a/src/Xamarin.Android.Build.Tasks/Xamarin.Android.Common.targets#L315-L336
[depot_tools]: http://commondatastorage.googleapis.com/chrome-infra-docs/flat/depot_tools/docs/html/depot_tools_tutorial.html
[powershell_script]: https://github.com/jonathanpeppers/HelloWorld/blob/39e2854f6ca39c0941fb8bd6f2a16d8b7663003e/build.ps1
[d8andr8_zip]: https://github.com/xamarin/xamarin-android/files/2470385/d8andr8.zip
