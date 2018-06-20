
# How to deal with new API Level

## This documentation is incomplete

In Xamarin ages, we used to have (more complete) API upgrade guide internally. But since then we switched to new xamarin-android repository which entirely changed the build system from Makefile to MSBuild solution, as well as the managed API for manipulating Android SDK, the old documentation almost does not make sense anymore. Even though I am writing this documentation, I don't know everything required (nor those who changed the build system didn't care about API upgrades).

Hence, this documentation is written from the ground,  exploring everything around.

And since the build system has changed between the first preview of Android O and the latest Android O preview (3), and it is quite possible that the build system changes over and over again, it might still not make much sense in the future.

Things also changed after O. P bindings were generated in the different way than that for O.

## Quick list of the related components

- SDK components (build-tools/android-toolchain)
- API (parameter names) description (external/Java.Interop/build-tools/xamarin-android-docimporter-ng)
- Mono.Android API (src/Mono.Android)
- Mono.Android API enumification (build-tools/enumification-helpers)
- new AndroidManifest.xml elements and attributes (build-tools/manifest-attribute-codegen)

It often happens that a new API binding uncovers "generator" issues, or requires new features (e.g. actions for default interface methods).

## Steps

Anyhow, this commit would tell you what needs to be changed when the new API preview arrives (and that becomes stable): https://github.com/xamarin/xamarin-android/commit/8ce2537

For reference, this was for O: https://github.com/xamarin/xamarin-android/pull/642

1) Add/update new download to build-tools/android-toolchain.

The new API archive should be found on the web repository description
that Google Android SDK Manager uses (which can be now shown as part
of SDK Manager options in Android Studio).

As of Android P, it is at https://dl-ssl.google.com/android/repository/repository2-1.xml . It used to be different repository description URL, and it will be different URL in the future.

2) Create and add api-P.params.txt.

It can be done from within `external/Java.Interop/build-tools/xamarin-android-docimporter-ng` directory. See `README.md` in that directory for details. You will have to make changes to Makefile in that directory to generate it. We used to parse DroidDoc, but since google had stopped shipping docs in timely manner and scraping docs is very error prone, we switched to "stubs" source parser.

You might be forced to fix and/or add new features to Java source parsers. (You don't have to listen to people who say you can implement full Java parser. We don't need that and it's waste of development resource.)

Once api-P.params.txt is successfully generated, then copy it to `src/Mono.Android/Profiles`.

3) Make changes to Configuration.props, android-toolchain.projitems, BuildEverything.mk etc.

There are many configuration files that holds API definitions. Since the build system is an assorted hacks that don't care consistency, definitions are everywhere. Check the commit mentioned above and edit those files.

Usually preview API is given some unconfirmed number for the target framework (e.g. P API, which ended up to be 9.0, was initially given 8.1.99 where O was 8.1).

There is some assumption that an API Level is a number, whereas a "platform ID" can be possibly alphabets. For P preview, API Level was `28` while platform ID was `P`. There are couple of definitions that need to be declared if and only if those two are different.

When the API became final, those preview-only property values have to be reverted back to the stable state.

4) Generate new API binding (and review the API updates).

Once you are done with all above, then you are ready to try to build `Mono.Android.dll`. `make API_LEVEL=P` would generate the target API binding (might be `API_LEVEL=28`).

Mono.Android.dll build is somewhat different from normal Android Binding projects, but the basic process is the same. First `class-parse` extracts API definition from `android.jar`, then `api-xml-adjuster` fixes API definitions so that it can consistently apply `metadata` (which is `Metadata.xml` in binding project templates) as well as `map.csv` and `methodmap.csv` (which are `EnumFields.xml` and `EnumMethods.xml` in binding project templates), then ... `generator` generates the C# sources.

What's different from normal bindings is between `api-xml-adjuster` and `generator`. We "merge" API various descriptions for all the supported API levels (10, 15, 16, ... 28) so that we provide consistent (non-breaking) APIs across API Levels. It is done by a tool called `api-merge`.

`generator` step usually fails at first, and you are supposed to make some changes to `src/Mono.Android/metadata` to resolve those API generation glitches (in the same spirit as normal Android Binding projects). "Troubleshooting Bindings" document would be helpful for you. Note that API fixup has to be done against `src/Mono.Android/obj/Debug/android-P/mcw/api.xml` which is the result of `api-merge` step.

5) enumification

See `build-tools/enumification-helpers/README`. Usually it takes many days to complete...

Enumification work can be delayed and only the final API has to be enumified.

6) new AndroidManifest.xml elements and attributes

`build-tools/manifest-attribute-codegen/manifest-attribute-codegen.cs` can be compiled to a tool that collects all Manifest elements and attributes with the API level since when each of them became available. New members are supposed to be added to the existing `(FooBar)Attribute.cs` and `(FooBar)Attribute.Partial.cs` in `src/Mono.Android` and `src/Xamarin.Android.Build.Tasks` respectively.

Note that there are documented and undocumented XML nodes, and we don't have to deal with undocumented ones.

Android P introduced no documented XML artifact.
