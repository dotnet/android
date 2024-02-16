# MSBuild Best Practices

This guide is a work-in-progress, but really has two main goals:
- What are good MSBuild practices, in general?
- What are good MSBuild practice in relation to what we already have
  going on in Xamarin.Android MSBuild targets?

## Debugging MSBuild Tasks

One thing that is very useful is the ability to debug your Tasks while
they are being run on a build process. This is possible thanks to the
`MSBUILDDEBUGONSTART` environment variable. When set to `2` this will
force MSBuild to wait for a debugger connection before continuing.
You will see the following prompt.

```dotnetcli
Waiting for debugger to attach (dotnet PID 13001).  Press enter to continue...
```

You can then use VS or VSCode to attach to this process and debug you tasks.

In the case of .NET Android we need to do a couple of thing first though. Firstly
we need to disable the use of `ILRepacker` on the `Xamarin.Android.Build.Tasks`
assembly. This is because `ILRepacker` does NOT handle debug symbols very well.
Assemblies it generates seem to be JIT optimized so the debugger will not load
the symbols. A new MSBuild property has been introduced to disable this feature
while debugging. `_ILRepackEnabled` can be set as an environment variable which
MSBuild will pickup. 

```dotnetcli
make prepare && _ILRepackEnabled=false make jenkins
```

This will disable the `ILRepacker` for the build.

You can then start your test app with the `dotnet-local` script (so it uses your build)

```dotnetcli
MSBUILDDEBUGONSTART=2 ~/<some xamarin.android checkout>/dotnet-local.sh build -m:1
```

Once MSBuild starts it will print the following

```dotnetcli
Waiting for debugger to attach (dotnet PID xxxx).  Press enter to continue...
```

You need to copy the PID value so we can use this in the IDE. For Visual Studio you can use the `Attach to Process` menu option, while you have the Xamarin.Android.sln solution open. For VSCode open the workspace then use the `Debug MSBuild Task` Run and Debug option. You will be prompted for the PID and it will then connect.

Once connection go back to your command prompt and press ENTER so that the MSBuild process can continue.

You will be able to set breakpoints in Tasks (but not Targets) and step through code from this point on.

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

## Conditions

You can skip an MSBuild `<Target/>` or task with a `Condition` such
as:

```xml
<Target Name="Foo" Condition=" '$(Bar)' == 'true' ">
  <!-- ... -->
</Target>
```

If you want to skip the target if an item group is empty, you might be
tempted to do:

```xml
<Target Name="Foo" Condition=" '@(MyItems)' != '' ">
  <!-- ... -->
</Target>
```

If you think about what this does, it's doing a `string.Join()` on
`@(MyItems)` to compare if it matches an empty string. Luckily MSBuild
has a "fast path" for evaluating against an empty string, but it still
can generate the log message:

```
Target "Foo" skipped, due to false condition; ('@(MyItems)' != '') was evaluated as ('A;B;C' != '')
```

If `@(MyItems)` was 100 full paths to files, this would be a long log
message!

The solution is you should generally do this instead:

```xml
<Target Name="Foo" Condition=" '@(MyItems->Count())' != '0' ">
  <!-- ... -->
</Target>
```

This causes MSBuild to always generate a reasonable log message:

```
Target "Foo" skipped, due to false condition; ('@(MyItems->Count())' != '0') was evaluated as ('100' != '0')
```

`->Count()` will return 0 even if the item group does not exist. See
the [MSBuild Documentation][itemfunctions] for details.

Some links around the logging behavior:

* https://github.com/dotnet/msbuild/issues/5315
* https://github.com/dotnet/msbuild/pull/5553
* https://github.com/dotnet/roslyn/pull/46445

[itemfunctions]: https://docs.microsoft.com/visualstudio/msbuild/item-functions

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

## Building Partially

In addition to MSBuild targets skipping completely, there is a way for
them to run *partially*. The concept is that your target has a 1-to-1
mapping from a set of `Inputs` to `Outputs` and the actual work done
in between can be performed on each file individually.

Let's imagine we had a target that generates a documentation file for
a list of `*.java` files:

```xml
<Target Name="_GenerateDocumentation"
    Inputs="@(_JavaFiles)"
    Outputs="@(_JavaFiles->'$(DocsDirectory)%(Filename).md')">
  <GenerateDocumentation
      SourceFiles="@(_JavaFiles)"
      DestinationFiles="@(_JavaFiles->'$(DocsDirectory)%(Filename).md')"
  />
</Target>
```

This target uses a given `$(DocsDirectory)` to generate a tree of
documentation files for each `.java` file. We use an [item group
transform][msbuild-transforms] to get a list of files in another
directory.

So let's imagine you edited one `Foo.java` file, on the next build:

- The `_GenerateDocumentation` target will run *partially*, saying
  `Foo.java` is newer than `Foo.md`.
- The `<GenerateDocumentation/>` MSBuild task will only receive inputs
  of the files that changed. `SourceFiles` will have one file, and
  `DestinationFiles` one file.

We could have also used a stamp file here, but in this case it is more
performant for incremental builds to build *partially*. When one
`.java` file changes, we only have to generate one documentation file.

Building partially also works, if you need to invalidate on additional
files, such as:

```xml
<Target Name="_GenerateDocumentation"
    Inputs="@(_JavaFiles);$(MSBuildAllProjects)"
    Outputs="@(_JavaFiles->'$(DocsDirectory)%(Filename).md')">
<!-- ... -->
</Target>
```

This is helpful if there is an MSBuild property that alters the output
of `<GenerateDocumentation/>`. A change to an MSBuild project file
will re-run the target completely. `$(MSBuildAllProjects)` is a list
of every MSBuild file imported during a build, MSBuild automatically
evaluates `$(MSBuildAllProjects)` since [MSBuild 16.0][allprojects].

> NOTE: You might consider using `@(_AndroidMSBuildAllProjects)`
> instead of `$(MSBuildAllProjects)` when working on the
> Xamarin.Android MSBuild targets. We have excluded the `*.csproj.user`
> file for performance reasons.

One pitfall, is this `_GenerateDocumentation` example *must* touch the
timestamps on all files in `Outputs` -- regardless if they were
actually changed. Otherwise, your target can get into a state where it
will never be skipped again.

Read more about building *partially* on the [official MSBuild
docs][msbuild-partial].

[allprojects]: http://www.panopticoncentral.net/2019/07/12/msbuildallprojects-considered-harmful/
[msbuild-partial]: https://docs.microsoft.com/en-us/visualstudio/msbuild/how-to-build-incrementally

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

It depends, but probably not.

Let's look at a simple example of why you might not want to 
do so. Let's assume we have a target that runs the linker after `Build`:

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
Consider what happens if the target fails in that case, and consider 
using `$(MSBuildLastTaskResult)` if available to check for the last 
task execution state (see [docs](https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-reserved-and-well-known-properties?view=vs-2019) 
on possible states for that property).

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
string value = BuildEngine4.GetRegisteredTaskObject (key, RegisteredTaskObjectLifetime.Build) as string;
```

Or if you want to cache across multiple-builds, use
`RegisteredTaskObjectLifetime.AppDomain` instead.

> NOTE!
> Use `as` for casts to avoid any unexpected `InvalidCastException`'s.
> Use the `RegisterTaskObjectAssemblyLocal()` or
> `GetRegisteredTaskObjectAssemblyLocal()` extension methods within
> the Xamarin.Android codebase.

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

## Xamarin.Android MSBuild Task base classes

We have a few base classes to simplify error handling, `async` /
`await` usage, etc.

`AndroidTask` is a plain `Task`, override `RunTask` and use it as you
would `Task.Execute()`:

```csharp
public class MyTask : AndroidTask
{
    // Prefix for XAMYT0000 error codes: choose unique chars
    public override string TaskPrefix => "MYT";

    public override bool RunTask ()
    {
        // Implementation
        return !Log.HasLoggedErrors;
    }
}
```

The benefit here is that if an unhandled exception is thrown, `MyTask`
will automatically generate proper error codes.

`AndroidAsyncTask` has an additional override for doing work on a
background thread:

```csharp
public async override System.Threading.Tasks.Task RunTaskAsync ()
{
    await DoSomethingExpensive ();
}
```

`RunTaskAsync` is already on a background thread, and is setup to do
the proper `Yield()`, `try`, `finally`, and `Reacquire()` calls needed
for MSBuild.

You might also leverage the `WhenAll` extension method:

```csharp
public async override System.Threading.Tasks.Task RunTaskAsync ()
{
    await this.WhenAll (Files, DoWork);
}

[Required]
ITaskItem [] Files { get; set;}

void DoWork (ITaskItem file)
{
    // The actual work done in parallel
}
```

There are still some things to look out for with `AsyncTask`:

* Use full paths on the background thread, or make use of the
  `AsyncTask.WorkingDirectory` property. If the task is shifted to
  another MSBuild node, `Environment.CurrentDirectory` will not be
  what is expected.
* Use the `AsyncTask.Log*` helper methods for logging. Calling
  `Log.LogMessage` directly can cause hangs in the IDE.

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
