# Check-Api-Compatibility

**Check-Api-Compatibility** is a process to check that changes are not causing
APIs breakages on Mono.Android.dll
The process uses [Microsoft.DotNet.ApiCompat][mdac] tool in order to
test API compatibility between a two .NET assemblies
When testing, the tool will compare a contract (old assembly) against an implementation (new assembly).

The *contract* represents the API that's expected : for example a reference assembly or a previous version of an assembly. We could also called it the V1 assembly.

The *implementation* represents the API that's provided : for example the current version of an assembly. We could also called it the V2 assembly.

[mdac]: https://github.com/dotnet/arcade/tree/master/src/Microsoft.DotNet.ApiCompat

## Build Task

We have developed a build task that will wrap *Microsoft.DotNet.ApiCompat.exe* and perform required checks. The build task is called: *Xamarin.Android.Tools.BootstrapTasks.CheckApiCompatibility* and it is located under [BootstrapTasks][bst]
The task will expect the following parameters:

`ApiCompatPath` - Path where Microsoft.DotNet.ApiCompat nuget package is located.

`ApiLevel` - Api level just built.

`LastStableApiLevel` - The last stable api level.

`TargetImplementationPath` - Path to the implementation assembly. Based on this path we will generate the contract path by replace current version with previous version

`ApiCompatibilityPath` - Path to the Api Compatibility folder. On this folder we should find the acceptable-breakages files and the reference folder that contians the current Mono.Android.dll assembly.

The *task* is aware of the Api levels and what is the previous Api level for agiven version, so When building api version 2, the Task knows if previous version is the version 1.5 or 1.0 for instance.

## Usage

To use the build task we need to import it to our project. On *Mono.Android.targets* we add the line to reference the build task to our project.

Please see *Mono.Android.targets* as an example

`ApiCompatibilityFiles` - Are the files we would like to monitor for changes, this is done to prevent the task to run if it is not required. (there were no changes). See *tests/api-compatibility* directory to see the files we currently use.

`Touch Files` - It is our timestamp file to control if task should run.

[bst]: https://github.com/xamarin/xamarin-android/tree/master/build-tools/Xamarin.Android.Tools.BootstrapTasks/Xamarin.Android.Tools.BootstrapTasks

## ApiCompatibilityPath

On this path (*tests/api-compatibility*) we will place the files that contain all acceptable breakages for a given Api version.

for example:

```
acceptable-breakages-v7.0.txt
acceptable-breakages-v7.1.txt
acceptable-breakages-v8.0.txt
acceptable-breakages-v9.0.txt
acceptable-breakages-vReference.txt
```

`acceptable-breakages-v7.0.txt` - Contains any rules that are accept to be broken in relation to the previous target framework version of v7.0 (in this case v6.0)

Target framework version v6 used to have the follow piece of code:

```
[Obsolete (@"deprecated")]
public virtual unsafe Java.Lang.Object LastNonConfigurationInstance {
```

however target framework version v7, removed the `[Obsolete (@"deprecated")]`
```
public virtual unsafe Java.Lang.Object LastNonConfigurationInstance {
```

The Api compat tool would report that as an Api breakage and cause the build to fail.

However on file `acceptable-breakages-v7.0.txt` we added the folowing line:
```
CannotRemoveAttribute : Attribute 'System.ObsoleteAttribute' exists on 'Android.App.Activity.LastNonConfigurationInstance' in the contract but not the implementation.
```
that will cause the error to no cause a build failure.

When accepting break changes against the current reference assembly, use `acceptable-breakages-vReference.txt` file to control the items that are acceptable.

## Assembly reference

Besides checking against a previous Api Level, we should also check against the current Api Level. This will prevent we break the current last stable api level. In order to be able to do that we need to commit the Mono.Android.dll assembly to our repository.

We don't want to commit the "naked" assembly because it is quite large (around 30MB), so we should first:

`Strip IL code out of the assembly` -> We do that using a tool called `cil-strip.exe
On a mac machine run the follow command:

```
mono bin/Debug/lib/xamarin.android/xbuild/Xamarin/Android/cil-strip.exe Mono.Android.dll Mono.Android-out.dll
Mono CIL Stripper

26747392  Mono.Android.dll
19300864  Mono.Android-out.dll
```

Rename Mono.Android-out.dll to Mono.Android.dll, compress (zip Mono.Android.dll.zip Mono.Android.dll)

```
4145708  Mono.Android.dll.zip
```

commit the new zip file

