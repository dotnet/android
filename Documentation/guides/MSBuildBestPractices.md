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
  [Github #2247][github_issue]. Since you can't on a file being
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

# Best Practices for Xamarin.Android MSBuild targets

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
  <ItemGroup>
    <FileWrites Include="$(_AndroidStampDirectory)_ResolveLibraryProjectImports.stamp" />
  </ItemGroup>
</Target>
```

[msbuild]: https://github.com/Microsoft/msbuild/blob/master/documentation/wiki/Rebuilding-when-nothing-changed.md
[github_issue]: https://github.com/xamarin/xamarin-android/issues/2247
[clean]: https://github.com/Microsoft/msbuild/issues/2408#issuecomment-321082997