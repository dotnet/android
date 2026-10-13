### Breaking change: obsolete types removed

The following public types, which had been marked obsolete by .NET 9 or earlier,
are removed. This is a source and binary compatibility break; libraries that
reference these types must be updated and recompiled. Bindings for deprecated
Java and Android APIs remain available while those APIs are present in the
platform libraries.

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
  attributes.
- `Android.Media.AudioRecord.RoutingChangedEventArgs` and
  `Android.Media.AudioTrack.RoutingChangedEventArgs` were incorrectly generated
  and unused; the underlying platform API bindings are unchanged. Use
  `Android.Media.AudioRoutingOnRoutingChangedEventArgs` for the corresponding
  `RoutingChanged` events.

Previously compiled libraries that reference these types may also need to be
updated before they can be used with this version of .NET for Android.
