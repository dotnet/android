### Breaking change: obsolete APIs now produce errors

The following public APIs remain in the reference and runtime assemblies so
previously compiled libraries can still resolve their references. They are
marked obsolete with `error: true`, so newly compiled C# code cannot use them.
Bindings for deprecated Java and Android platform APIs remain available while
those APIs are present in the platform libraries.

- `Android.Runtime.PreserveAttribute` is replaced by
  `System.Diagnostics.CodeAnalysis.DynamicDependencyAttribute`.
- `Android.Runtime.TypeManager` is replaced by `Java.Interop.TypeManager`.
- `Android.LinkerSafeAttribute` is replaced by
  `[assembly: System.Reflection.AssemblyMetadata("IsTrimmable", "True")]`.
- `Java.Interop.AndroidEventHelper` is replaced by `Java.Interop.EventHelper`.
- `Android.Widget.ItemEventArgs` is replaced by
  `Android.Widget.AdapterView.ItemClickEventArgs`.
- `Java.Interop.DoNotPackageAttribute` is replaced by the
  `@(AndroidExternalJavaLibrary)` MSBuild item group.
- `Android.ReferenceFilesAttribute`,
  `Android.IncludeAndroidResourcesFromAttribute`,
  `Android.NativeLibraryReferenceAttribute`, and
  `Java.Interop.JavaLibraryReferenceAttribute` have no direct replacement.
  Their functionality has been unsupported since .NET for Android 10.2;
  update affected libraries to supported NuGet packages and remove these
  attributes. Existing binaries can still resolve the attribute types.
- `Android.Media.AudioRecord.RoutingChangedEventArgs` and
  `Android.Media.AudioTrack.RoutingChangedEventArgs` were incorrectly generated
  and unused; the underlying platform API bindings are unchanged. Use
  `Android.Media.AudioRoutingOnRoutingChangedEventArgs` for the corresponding
  `RoutingChanged` events.

This change prevents new source from using these obsolete APIs without
breaking type resolution for existing compiled libraries. It does not restore
functionality that was already unsupported.
