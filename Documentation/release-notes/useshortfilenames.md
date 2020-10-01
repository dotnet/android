### `UseShortFileNames=false` is no longer supported

In previous versions of Xamarin.Android, you could set
`$(UseShortFileNames)` in your `.csproj` file:

```xml
<PropertyGroup>
  <UseShortFileNames>false</UseShortFileNames>
</PropertyGroup>
```

This would tell Xamarin.Android's MSBuild targets to use the "long"
folder names such as:

* `obj\Debug\lp` -> `__library_projects__`
* `obj\Debug\lp\*\jl` -> `library_project_imports`
* `obj\Debug\lp\*\nl` -> `native_library_imports`

This was useful when `$(UseShortFileNames)` was a new feature, giving
developers a way to "opt out" if they hit a bug. However,
`$(UseShortFileNames)` has defaulted to `true` since ~July 2017 to
help the [MAX_PATH][0] limit on Windows.

This functionality has been removed from Xamarin.Android. Short file
names will be used going forward.

[0]: https://docs.microsoft.com/windows/win32/fileio/naming-a-file#maximum-path-length-limitation
