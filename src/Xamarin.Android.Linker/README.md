# .NET 5 Linker Exploration

Started with a new `Xamarin.Android.Linker` assembly with:

```xml
<PackageReference Include="Microsoft.NET.ILLink" Version="5.0.0-preview.3.20203.2" />
```

## Missing APIs

* `LinkContext` is missing `Log*` methods and we also would need a
  `MessageImportance` enum.

* `LinkContext` is missing a `GetAssemblies` method. Maybe there is
  another option? This is used to find the `Mono.Android.dll` assembly
  in steps such as `FixAbstractMethods`.

* `FixAbstractMethods` has a `LinkContext.SafeReadSymbols` call:

https://github.com/xamarin/xamarin-android/blob/545c2239b27dfaf069613e18690192a2f3d76c74/src/Xamarin.Android.Build.Tasks/Linker/MonoDroid.Tuner/FixAbstractMethodsStep.cs#L38

Maybe can just be removed?

* There is usage of a `Mono.Tuner.Profile` class. Do we need to change
  how this works?

## ISubStep and BaseSubStep

The following steps use `ISubStep` and `BaseSubStep` and appear to be
needed in .NET 5:

```xml
<Compile Remove="..\Xamarin.Android.Build.Tasks\Linker\MonoDroid.Tuner\MarkJavaObjects.cs" />
<Compile Remove="..\Xamarin.Android.Build.Tasks\Linker\MonoDroid.Tuner\PreserveApplications.cs" />
<Compile Remove="..\Xamarin.Android.Build.Tasks\Linker\MonoDroid.Tuner\PreserveExportedTypes.cs" />
<Compile Remove="..\Xamarin.Android.Build.Tasks\Linker\MonoDroid.Tuner\PreserveJavaExceptions.cs" />
<Compile Remove="..\Xamarin.Android.Build.Tasks\Linker\MonoDroid.Tuner\PreserveJavaTypeRegistrations.cs" />
```

## Remaining Errors

```
"src\Xamarin.Android.Linker\Xamarin.Android.Linker.csproj" (default target) (1) ->
(CoreCompile target) -> 
  Mono.Tuner\Extensions.cs(24,37): error CS1061: 'LinkContext' does not contain a definition for 'GetAssemblies' and no accessible extension method 'GetAssemblies' accepting a first argument of type 'LinkContext' could be found (are you missing a using directive or an assembly reference?) [src\Xamarin.Android.Linker\Xamarin.Android.Linker.csproj]
  src\Xamarin.Android.Build.Tasks\Linker\MonoDroid.Tuner\FixAbstractMethodsStep.cs(35,84): error CS0103: The name 'MessageImportance' does not exist in the current context [src\Xamarin.Android.Linker\Xamarin.Android.Linker.csproj]
  src\Xamarin.Android.Build.Tasks\Linker\MonoDroid.Tuner\FixAbstractMethodsStep.cs(35,72): error CS1061: 'LinkContext' does not contain a definition for 'LogMessage' and no accessible extension method 'LogMessage' accepting a first argument of type 'LinkContext' could be found (are you missing a using directive or an assembly reference?) [src\Xamarin.Android.Linker\Xamarin.Android.Linker.csproj]
  src\Xamarin.Android.Build.Tasks\Linker\MonoDroid.Tuner\FixAbstractMethodsStep.cs(38,13): error CS1061: 'LinkContext' does not contain a definition for 'SafeReadSymbols' and no accessible extension method 'SafeReadSymbols' accepting a first argument of type 'LinkContext' could be found (are you missing a using directive or an assembly reference?) [src\Xamarin.Android.Linker\Xamarin.Android.Linker.csproj]
  src\Xamarin.Android.Build.Tasks\Linker\MonoDroid.Tuner\StripEmbeddedLibraries.cs(30,15): error CS1061: 'LinkContext' does not contain a definition for 'LogMessage' and no accessible extension method 'LogMessage' accepting a first argument of type 'LinkContext' could be found (are you missing a using directive or an assembly reference?) [src\Xamarin.Android.Linker\Xamarin.Android.Linker.csproj]
  src\Xamarin.Android.Build.Tasks\Linker\MonoDroid.Tuner\FixAbstractMethodsStep.cs(94,11): error CS0103: The name 'Profile' does not exist in the current context [src\Xamarin.Android.Linker\Xamarin.Android.Linker.csproj]
  src\Xamarin.Android.Build.Tasks\Linker\MonoDroid.Tuner\FixAbstractMethodsStep.cs(94,47): error CS0103: The name 'Profile' does not exist in the current context [src\Xamarin.Android.Linker\Xamarin.Android.Linker.csproj]
  src\Xamarin.Android.Build.Tasks\Linker\MonoDroid.Tuner\FixAbstractMethodsStep.cs(295,12): error CS1061: 'LinkContext' does not contain a definition for 'LogMessage' and no accessible extension method 'LogMessage' accepting a first argument of type 'LinkContext' could be found (are you missing a using directive or an assembly reference?) [src\Xamarin.Android.Linker\Xamarin.Android.Linker.csproj]
  src\Xamarin.Android.Build.Tasks\Linker\MonoDroid.Tuner\FixAbstractMethodsStep.cs(300,37): error CS1061: 'LinkContext' does not contain a definition for 'GetAssemblies' and no accessible extension method 'GetAssemblies' accepting a first argument of type 'LinkContext' could be found (are you missing a using directive or an assembly reference?) [src\Xamarin.Android.Linker\Xamarin.Android.Linker.csproj]

    5 Warning(s)
    9 Error(s)
```
