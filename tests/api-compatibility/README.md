# Check-Api-Compatibility

**Check-Api-Compatibility** is a process to check that changes are not causing
APIs breakages in `Mono.Android.dll`.  The process uses the
[Microsoft.DotNet.ApiCompat][mdac] tool in order to test API compatibility
between a two .NET assemblies.  When testing, the tool will compare a *contract*
(old assembly) against an *implementation* (new assembly).

The *contract* represents the API that's expected: for example a reference
assembly or a previous version of an assembly.  We could also called it the
V1 assembly.

The *implementation* represents the API that's provided: for example the
current version of an assembly.  We could also called it the V2 assembly.

[mdac]: https://github.com/dotnet/arcade/tree/master/src/Microsoft.DotNet.ApiCompat


## Update Contract Assembly

To update the contract assembly, run the `UpdateMonoAndroidContract` target
and provide the `$(ContractAssembly)` MSBuild property.  `$(ContractAssembly)`
should be the path to the new contract assembly to use:

    dotnet msbuild Xamarin.Android.sln -t:UpdateMonoAndroidContract "-p:ContractAssembly=C:/code/xamarin-android-backport/bin/Debug/lib/packs/Microsoft.Android.Ref.34/34.99.0/ref/net9.0/Mono.Android.dll"

Note: using the assembly in the `ref` directory means it has already had IL stripped
and is just API.

## Build Task

We have developed a build task that will wrap *Microsoft.DotNet.ApiCompat.exe*
and perform the required checks.  The build task is called
`<Xamarin.Android.Tools.BootstrapTasks.CheckApiCompatibility/>` and it is
located under [BootstrapTasks][bst].

`<CheckApiCompatibility/>` expects the following parameters:

  * `ApiCompatPath`: Path where `Microsoft.DotNet.ApiCompat` nuget package
    is located.

  * `ApiLevel`: API-level just built. 

  * `LastStableApiLevel`: The last stable api level.

  * `TargetImplementationPath`: Path to the implementation assembly.  Based on
    this path we will generate the contract path by replace current version with
    previous version

  * `ApiCompatibilityPath`: Path to the Api Compatibility folder.  In this
    folder we should find the acceptable-breakages files and the reference
    folder that contains the current `Mono.Android.dll` assembly.

The *task* is aware of the API levels and what is the previous API level for a
given `$(TargetFrameworkVersion)`, so when building v2.0, the task knows if
previous version is v1.5 or v1.0 for example.


## Acceptable Breakages

When *Microsoft.DotNet.ApiCompat* executes, it prints out all API breakages it
encounters.

To *ignore* a reported API breakage, copy the error message produced by
*Microsoft.DotNet.ApiCompat* into an appropriate
`tests/api-compatibility/acceptable-breakages-*.txt` file.  The `*` is replaced
with the *current* `$(TargetFrameworkVersion)` value being processed.

For example, when comparing `Mono.Android.dll` v6.0 against v7.0, the following
warning is reported:

	CannotRemoveAttribute : Attribute 'System.ObsoleteAttribute' exists on 'Android.App.Activity.LastNonConfigurationInstance' in the contract but not the implementation.

This warning is produced because in v6.0 (API-23) we produced:

```csharp
[Obsolete (@"deprecated")]
public virtual unsafe Java.Lang.Object LastNonConfigurationInstance {
```

because [`android.app.Activity.getLastNonConfigurationInstance`][aglnci] was
deprecated in API-15, so we declared it `[Obsolete]`.

[aglnci]: https://developer.android.com/reference/android/app/Activity#getLastNonConfigurationInstance()


Meanwhile in v7.0 (API-24) we stopped emitting `[Obsolete]`:

```csharp
public virtual unsafe Java.Lang.Object LastNonConfigurationInstance {
```

This is because Google *un-deprecated* the method!

To ignore this warning, `tests/api-compatibility/acceptable-breakages-v7.0.txt`
contains the above message text, which causes `<CheckApiCompatibility/>` to
ignore the reported warning.

When accepting break changes against the current reference assembly, use
`tests/api-compatibility/acceptable-breakages-vReference.txt` to control the
items that are acceptable.


## Assembly reference

Besides checking against a previous API Level, we should also check against the
current API Level.  This will prevent us from breaking the current stable
binding.  In order to be able to do that we need to commit the `Mono.Android.dll`
assembly to our repository.

We don't want to commit the "naked" assembly because it is quite large (around 30MB),
so we should first strip IL code from the assembly by using `cil-strip.exe`, for
example on macOS:


```
$ mono bin/Debug/lib/xamarin.android/xbuild/Xamarin/Android/cil-strip.exe Mono.Android.dll Mono.Android-out.dll
Mono CIL Stripper

26747392  Mono.Android.dll
19300864  Mono.Android-out.dll
```

Rename `Mono.Android-out.dll` to `Mono.Android.dll`, then compress into a `.zip` file:

```
zip Mono.Android.dll.zip Mono.Android.dll
```

Commit the new `Mono.Android.dll.zip` file into
`tests/api-compatibility/reference/Mono.Android.dll.zip`.
