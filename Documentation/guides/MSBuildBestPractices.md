# MSBuild Best Practices

This guide is a work-in-progress, but really has two main goals:
- What are good MSBuild practices, in general?
- What are good MSBuild practice in relation to what we already have
  going on in Xamarin.Android MSBuild targets?

## Naming

MSBuild targets, properties, and item groups are prefixed with an
underscore, unless they are considered a public-facing API. The reason
for this convention is that MSBuild does not have its own concepts of
visibility (public vs private). Hence everything in MSBuild is
basically a "global variable". Prefixing with an underscore is a
subtle hint to the consumer, "we might rename this thing!".

So for example:

`$(AndroidEnableProguard)` - is a public property that developer's
use to enable proguard (Java code shrinking) in their applications.

`@(ProguardConfiguration)` - is a public item group (or build action)
developers use to add additional proguard configuration to their
projects.

`SignAndroidPackage` - is a well-known (and widely used) public
MSBuild target used for producing an APK.

If we removed any of these, this would be the same as creating
breaking API changes. We should be careful when adding new public
properties, targets, etc., as we would likely need to support them
into oblivion. However, we might choose to "safely" deprecate them
in a way that makes sense.

## Item Group Transforms

MSBuild has a widely used feature where you can create a one-to-one
mapping from an `<ItemGroup/>` to a new `<ItemGroup/>`. The syntax for
this looks like:

```xml
<ItemGroup>
  <_DestinationFiles Include="@(_SourceFiles->'$(SomeDirectory)%(Filename)%(Extension)')" />
</ItemGroup>
```

This takes a list of files, and creates a desired destination path for
each file in `$(SomeDirectory)`. The `%(Filename)` and `%(Extension)`
item metadata is used to get the filename of the source file. See the
MSBuild documentation on [transforms][msbuild-transforms] and
[well-known item metadata][msbuild-metadata] for more info.

One thing to note here, is we shouldn't have multiple transforms
within the same target:

```xml
<Target Name="_CopyPdbFiles"
    Inputs="@(_ResolvedPortablePdbFiles)"
    Outputs="$(_AndroidStampDirectory)_CopyPdbFiles.stamp"
    DependsOnTargets="_ConvertPdbFiles">
  <CopyIfChanged
      SourceFiles="@(_ResolvedPortablePdbFiles)"
      DestinationFiles="@(_ResolvedPortablePdbFiles->'$(MonoAndroidLinkerInputDir)%(Filename)%(Extension)')"
  />
  <Touch Files="$(_AndroidStampDirectory)_CopyPdbFiles.stamp" AlwaysCreate="True" />
  <ItemGroup>
    <FileWrites Include="@(_ResolvedPortablePdbFiles->'$(MonoAndroidLinkerInputDir)%(Filename)%(Extension)')" />
  </ItemGroup>
</Target>
```

Running this transformation twice:

```
@(_ResolvedPortablePdbFiles->'$(MonoAndroidLinkerInputDir)%(Filename)%(Extension)')
```

Would be like generating the same `string[]` twice in C#, in the same method.

The target could be better written as:

```xml
<Target Name="_CopyPdbFiles"
    Inputs="@(_ResolvedPortablePdbFiles)"
    Outputs="$(_AndroidStampDirectory)_CopyPdbFiles.stamp"
    DependsOnTargets="_ConvertPdbFiles">
  <ItemGroup>
    <_CopyPdbFilesDestinationFiles Include="@(_ResolvedPortablePdbFiles->'$(MonoAndroidLinkerInputDir)%(Filename)%(Extension)')" />
  </ItemGroup>
  <CopyIfChanged SourceFiles="@(_ResolvedPortablePdbFiles)" DestinationFiles="@(_CopyPdbFilesDestinationFiles)" />
  <Touch Files="$(_AndroidStampDirectory)_CopyPdbFiles.stamp" AlwaysCreate="True" />
  <ItemGroup>
    <FileWrites Include="@(_CopyPdbFilesDestinationFiles)" />
  </ItemGroup>
</Target>
```

Additionally, if the new `@(_CopyPdbFilesDestinationFiles)` is not
meant to be used from outside the target, it should be prefixed with
an underscore and have a name specific to the target. The
`@(_CopyPdbFilesDestinationFiles)` name is a reasonable length, but an
abbreviation could be used if the name is quite long, such as:
`@(_CPFDestinationFiles)`. `_CPF` denotes a private value within the
`_CopyPdbFiles` target.

[msbuild-transforms]: https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-transforms
[msbuild-metadata]: https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-well-known-item-metadata

## Incremental Builds

The MSBuild Github repo has some [documentation][msbuild] on this
subject, but there isn't a lot of content out there on how things
should be done in MSBuild targets so they build *incrementally*:
things aren't "rebuilding" all the time.

First let's look at what a simple MSBuild target looks like that does
*not* build incrementally. Let's assume we have a simple target that
compiles Java code to a `jar` file:

```xml
<Target Name="_CompileJava">
  <Javac InputFiles="@(_JavaFiles)" OutputFile="$(_JarFile)" />
</Target>
```

Here I've made up a fictional `<Javac/>` MSBuild task that takes
`*.java` files and runs `javac` to produce a `jar` file. I'm assuming
an item group of `@(_JavaFiles)` exist for simplicity, and the
`$(_JarFile)` property is set to a valid path.

So if we ran this target, it would run every time: the target isn't
setup to build incrementally *at all*. To do this properly, MSBuild
has the concept of `Inputs` and `Outputs` of a target. If the
timestamps of the `Outputs` are all *newer* than the `Inputs`, MSBuild
will skip the target completely.

```xml
<Target Name="_CompileJava"
    Inputs="@(_JavaFiles)"
    Outputs="$(_JarFile)">
  <Javac InputFiles="@(_JavaFiles)" OutputFile="$(_JarFile)" />
</Target>
```

Now with this change, the `_CompileJava` target will be skipped unless
a Java file has been changed (its timestamp altered).

## Stamp Files

So let's look at another example:

```xml
<Target Name="_GenerateJavaSources">
  <GenerateJava 
      InputFiles="@(_AssemblyFiles)"
      OutputDirectory="$(_JavaIntermediateDirectory)"
  />
</Target>
```

In this case, a `GenerateJava` task will look at a set of .NET
assemblies and generate Java source code in an output directory. We
have no idea what files will be output from this MSBuild task *ahead
of time*, so what are the `Outputs`? How can it possibly build
incrementally?

Here is a good case to use a "stamp" file (also called a "flag" file).
We can create a 0-byte file on disk, which is merely used as a
timestamp for incremental builds.

```xml
<Target Name="_GenerateJavaSources"
    Inputs="@(_AssemblyFiles)"
    Outputs="$(_GenerateJavaStamp)">
  <GenerateJava 
      InputFiles="@(_AssemblyFiles)"
      OutputDirectory="$(_JavaIntermediateDirectory)"
  />
  <Touch Files="$(_GenerateJavaStamp)" AlwaysCreate="True" />
</Target>
```

Here we assume that `$(_GenerateJavaStamp)` is a path to a file such
as `obj\Debug\GenerateJava.stamp`. We can use the built-in `<Touch/>`
MSBuild task to create a 0-byte file and update its timestamp. Now the
`_GenerateJavaSources` target will only run on subsequent runs if an
assembly file is *newer* than the stamp file.

Other times it is good to use "stamp" files:
- You have files that are not *always* updated, such as mentioned in
  [Github #2247][github_issue]. Since you can't rely on a file being
  updated, it might be desirable to run the `<Touch />` command within
  the target or use a stamp file.
- The outputs are *many* files. Since MSBuild has to hit the disk to
  read timestamps of all these files, it may be a performance
  improvement to use a stamp file. Profile build times before and
  after to be sure.

## FileWrites and IncrementalClean

Generally, the place to put intermediate files during a build is
`$(IntermediateOutputPath)`, which is by default set to
`obj\$(Configuration)\` and always has a trailing slash.

MSBuild has a target, named `IncrementalClean`, that might be the bane
of our existance...

First, I would read up on the [Clean target][clean] and understand how
`Clean` really works within MSBuild. `Clean` in short, should delete
any file produced by a previous build. It does *not* simply delete
`bin` and `obj`.

`IncrementalClean` has the job of deleting "extra files" that might be
hanging out in `$(IntermediateOutputPath)`. So it might happily go
delete your stamp file, and completely break your incremental build!

The way to properly solve this problem, is to add intermediate files
to the `FileWrites` item group. And so our previous target would
properly be written as:

```xml
<Target Name="_GenerateJavaSources"
    Inputs="@(_AssemblyFiles)"
    Outputs="$(_GenerateJavaStamp)">
  <GenerateJava 
      InputFiles="@(_AssemblyFiles)"
      OutputDirectory="$(_JavaIntermediateDirectory)"
  />
  <Touch Files="$(_GenerateJavaStamp)" AlwaysCreate="True" />
  <ItemGroup>
    <FileWrites Include="$(_GenerateJavaStamp)" />
  </ItemGroup>
</Target>
```

Also note that the `<ItemGroup>` will still be evaluated here, even
though the `_GenerateJavaSources` target might be skipped due to
`Inputs` and `Outputs`.

However, for example, the following would be *wrong*!

```xml
<Target Name="_GenerateJavaSources"
    Inputs="@(_AssemblyFiles)"
    Outputs="$(_GenerateJavaStamp)">
  <GenerateJava 
      InputFiles="@(_AssemblyFiles)"
      OutputDirectory="$(_JavaIntermediateDirectory)"
  />
  <Touch Files="$(_GenerateJavaStamp)" AlwaysCreate="True">
    <!-- WRONG, nope! don't do this! -->
    <Output TaskParameter="TouchedFiles" ItemName="FileWrites" />
  </Touch>
</Target>
```

Using `<Output/>` allows the `TouchedFiles` output parameter to get
directly added to `FileWrites`.

This seems like it would be simpler and *better*, but
`$(_GenerateJavaStamp)` won't get added to `FileWrites` if this target
is skipped! `IncrementalClean` could delete the file and cause this
target to re-run on the next build!

## When to *not* use Inputs and Outputs?

In other words, when should a target *not* build incrementally?

- If the target merely reads from files, or locates files.
- If the target merely sets properties or adds to item groups.

In general, if a target *writes* files, it should be incremental. If
it needs to run every time in order to support other targets, do not
use `Inputs` or `Outputs`.

## Should I use BeforeTargets or AfterTargets?

NO!

Let's look at a simple example of why. Let's assume we have a target
that runs the linker after `Build`:

```xml
<Target Name="_LinkAssemblies" AfterTargets="Build">
  <LinkAssemblies ... />
</Target>
```

Let's imagine the case where the project has a C# compiler error.
`_LinkAssemblies` will run, regardless if `Build` succeeded or not!

This causes a confusing set of errors:

1. A `<LinkAssemblies/>` failure due to a missing assembly.
2. The real problem: a syntax error in C# code.

Instead you should use:
```xml
<PropertyGroup>
  <BuildDependsOn>
    $(BuildDependsOn);
    _LinkAssemblies;
  </BuildDependsOn>
</PropertyGroup>
```

Unfortunately, not all targets will have a `$(XDependsOn)` property.
In some cases, `BeforeTargets` or `AfterTargets` is the only option.
Consider what happens if the target fails in that case.

## Caching in MSBuild Tasks

There are two scenarios where you may want to do some kind of
in-memory caching:

1. You have some data you need to share between two MSBuild tasks--or
   perhaps an instance of the same MSBuild task in another project
   within the current build.
2. You have some data you want to persist in-memory between *builds*,
   while the same instance of the IDE is open.

Scenario 1 can be achieved with `static` variables, although as
mentioned on [Don't use Static in C#][static_csharp], shared `static`
state can have its own pitfalls. Avoiding `static` for caching is
probably a good idea.

Instead we can use an API provided by MSBuild:

```csharp
// To cache a value
string key = "foo";
BuildEngine4.RegisterTaskObject (key, "bar", RegisteredTaskObjectLifetime.Build, allowEarlyCollection: false);
// To retrieve a value
string value = (string)BuildEngine4.GetRegisteredTaskObject (key, RegisteredTaskObjectLifetime.Build);
```

Or if you want to cache across multiple-builds, use
`RegisteredTaskObjectLifetime.AppDomain` instead.

To *test* and validate your MSBuild task's use of
`RegisteredTaskObjectLifetime.AppDomain` you have two choices:

1. Test builds in the IDE. You can verify with diagnostic logging
   turned on or using the [Project System Tools][project_system] to
   view `.binlog` files from builds in the IDE.
2. Set the `%MSBUILDNOINPROCNODE%` environment variable to `1` and
   build command-line. This undocumented env var forces MSBuild to
   create an out-of-process reusable node unless `/nr:false` or
   `%MSBUILDDISABLENODEREUSE%=1`. You should see a leftover
   `MSBuild.exe` worker node when running builds command-line.
  
_NOTE: Option 2 only works on Windows. Mono / macOS does not have
an implementation of MSBuild out-of-process nodes yet._

### Other Notes on `RegisterTaskObject`

* Make sure your cache invalidates properly. Use a key that will be
  different if the proper environment change occurs: the attached
  `$(AdbTarget)`, a file path, version number, etc.
* Cache primitive values that will not use up a lot of memory.
  `string` or `Tuple<string,string>` are fine data types to use as
  keys and/or values.
* Consider if the cached data should just be cached on-disk instead.
  Is the data ephemeral? Will it be valid when restarting the IDE?

[static_csharp]: https://softwareengineering.stackexchange.com/questions/161222/dont-use-static-in-c
[project_system]: https://marketplace.visualstudio.com/items?itemName=VisualStudioProductTeam.ProjectSystemTools

# Best Practices for Xamarin.Android MSBuild targets

## Naming in Xamarin.Android targets

As mentioned [above](/MSBuildBestPractices.md#naming), a good amount
of consideration should be done before adding new public-facing
MSBuild properties. This is pretty clear when adding a new feature,
since an obvious feature flag will be needed to enable it.

The main thing to keep in mind here is that almost all of our
public-facing MSBuild properties should be prefixed with `Android`.
This is a good convention so it is easy to know which properties are
specific to Xamarin.Android, and this will prevent them from
conflicting with MSBuild properties from other products. All MSBuild
properties are effectively "global variables"...

## Stamp Files

From now on, we should try to put new stamp files in
`$(IntermediateOutputPath)stamp\` using the
`$(_AndroidStampDirectory)` property. This way we can be sure they are
deleted on `Clean` with a single call:

```xml
<RemoveDirFixed Directories="$(_AndroidStampDirectory)" Condition="Exists ('$(_AndroidStampDirectory)')" />
```

We should also name the stamp file the same as the target, such as:

```xml
<Target Name="_ResolveLibraryProjectImports"
    Inputs="..."
    Outputs="$(_AndroidStampDirectory)_ResolveLibraryProjectImports.stamp">
  <!-- ... -->
  <Touch Files="$(_AndroidStampDirectory)_ResolveLibraryProjectImports.stamp" AlwaysCreate="True" />
</Target>
```

Do we need `FileWrites` here? Nope. The `_AddFilesToFileWrites`
target takes care of it, so we can't as easily mess it up:

```xml
<Target Name="_AddFilesToFileWrites">
  <ItemGroup>
    <FileWrites Include="$(_AndroidStampDirectory)*.stamp" />
  </ItemGroup>
</Target>
```

## Legacy Code and XBuild

From time to time, we might find oddities in our MSBuild targets, that
might be around for one reason or another:

  - We might be doing something weird in order to support XBuild. We
    support XBuild no longer, yay!
  - The code just might have been around a while, and there wasn't a
    reason to change it.
  - There is a nuance to MSBuild we hadn't figured out yet (lol?).

Take, for instance, the following example:

```xml
<WriteLinesToFile
    File="$(IntermediateOutputPath)$(CleanFile)"
    Lines="@(_ConvertedDebuggingFiles)"
    Overwrite="false"
/>
```

The intent here is to replicate what happens with the `@(FileWrites)`
item group, by directly writing to this file. This
`<WriteLinesToFile/>` call likely "works" in some fashion, but is not
quite correct.

A couple problems with this approach with MSBuild:

  - This task won't run if the target is skipped!
  - How do we know MSBuild isn't going to overwrite this file?
  - On a subsequent build, this could append to the file *again*.

Really, who knows what weirdness could be caused by this?

For MSBuild, we should instead do:

```xml
<ItemGroup>
  <FileWrites Include="@(_ConvertedDebuggingFiles)" />
</ItemGroup>
```

Then we just let MSBuild and `IncrementalClean` do their thing.

## Run a Target Before IncrementalClean

If you have a target that needs to run before `IncrementalClean`, such
as:

```xml
<Target Name="_AddFilesToFileWrites">
  <ItemGroup>
    <FileWrites Include="$(_AndroidStampDirectory)*.stamp" />
  </ItemGroup>
</Target>
```

There is no `$(IncrementalCleanDependsOn)` property, what do you do?

Since using `BeforeTargets` and `AfterTargets` is a no-no, we have
modified `$(CoreBuildDependsOn)` so you can run a target *before*
`IncrementalClean`:

```xml
<PropertyGroup>
  <!--Add to this property as needed here-->
  <_BeforeIncrementalClean>
    _AddFilesToFileWrites;
  </_BeforeIncrementalClean>
  <CoreBuildDependsOn>
    $([MSBuild]::Unescape($(CoreBuildDependsOn.Replace('IncrementalClean;', '$(_BeforeIncrementalClean);IncrementalClean;'))))
  </CoreBuildDependsOn>
</PropertyGroup>
```

This is the current recommendation from the MSBuild team to run a
target before `IncrementalClean`.

See the following links about this problem:

  * [MSBuild Github Issue #3916][msbuild_issue]
  * [MSBuild Repro][msbuild_repro]

[msbuild]: https://github.com/Microsoft/msbuild/blob/master/documentation/wiki/Rebuilding-when-nothing-changed.md
[github_issue]: https://github.com/xamarin/xamarin-android/issues/2247
[clean]: https://github.com/Microsoft/msbuild/issues/2408#issuecomment-321082997
[msbuild_issue]: https://github.com/Microsoft/msbuild/issues/3916
[msbuild_repro]: https://github.com/jonathanpeppers/MSBuildIncrementalClean
