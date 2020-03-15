# Draft release notes

Any Pull Request that fixes a user-facing bug, implements a user-facing feature,
*intentionally* alters semantics, etc., should ideally *also* add a Markdown
file to this directory with a description of the change.

The "editorial audience" of these files is end-users of the product.
(Meanwhile, the "audience" of commit messages is other developers of the
xamarin-android repo.)

## File names

The name of each new file should be a "unique" string.  This could be the PR
number, if known, or the branch name used for the PR, or a GUID.  The filename
itself is largely irrelevant; the point is that it be sufficiently unique so as
to avoid potential merge conflicts with other PRs.

## Templates

These templates are listed roughly in order from more specific to less specific.
Use the most specific template that matches.  For example, for a build time
improvement, use the build time improvement template rather than the new feature
template or small improvement template.

### Template for feature infrastructure

  * Use this template for PRs that add only feature *infrastructure*.  This will
    let the team know that although the PR changes shipping code, the release
    notes can skip it until additional work is completed.

  * For comparison, for PRs that change only non-shipping code such as in
    `build-tools/` or `*/Tests/*`, don't worry about adding any note at all.

Example:

```
This change provides infrastructure for an upcoming feature.
```

### Template for build time or app performance improvement

  * Use a command form verb in the first sentence, as if you were telling
    someone to do something.

  * Include a before and after time comparison if possible.

  * Use one of the following headings:

      * `### Build and deployment performance`
      * `### App startup performance`
      * `### Android resource editing performance`

  * (Optional) If the PR already exists, include a link to the PR.

Examples:

```
### Build and deployment performance

  * [GitHub PR 3640](https://github.com/xamarin/xamarin-android/pull/3640):
    Use System.Reflection.Metadata rather than Cecil for
    `ResolveLibraryProjectImports`.  This reduced the time for the
    `ResolveLibraryProjectImports` task from about 4.8 seconds to about 4.5
    seconds for a small test Xamarin.Forms app on an initial clean build.
```

```
### Build and deployment performance

  * [GitHub PR 3535](https://github.com/xamarin/xamarin-android/pull/3535),
    [GitHub PR 3600](https://github.com/xamarin/xamarin-android/pull/3600):
    Add and switch to a new specialized `LinkAssembliesNoShrink` MSBuild task
    for Debug builds.  In the Debug scenario, where the managed linker is not
    set to remove any types, the managed linker only needs to run a quick step
    to adjust abstract methods in Xamarin.Android assemblies.  The new
    specialized task reduced the time for the `_LinkAssembliesNoShrink` target
    from about 340 milliseconds to about 10 milliseconds for a test
    Xamarin.Forms app when one line of a XAML file was changed between builds.
```

```
### App startup performance

  * [GitHub PR 3729](https://github.com/xamarin/xamarin-android/pull/3729):
    Initialize application logging and uncaught exception handling lazily.  This
    reduced the time to display the first screen of a small test Xamarin.Forms
    app from about 783 milliseconds to about 754 milliseconds for a Release
    configuration build on a Google Pixel 3 XL device.
```

```
### Android resource editing performance

  * [GitHub PR 3891](https://github.com/xamarin/xamarin-android/pull/3891):
    Avoid unnecessary changes to the *build.props* intermediate file during
    design-time builds so that the `UpdateGeneratedFiles` and
    `SetupDependenciesForDesigner` targets can skip the
    `_ResolveLibraryProjectImports` target.  This reduced the time for the
    `UpdateGeneratedFiles` target by about 300 milliseconds for a small app on a
    test system.  The `UpdateGeneratedFiles` target runs in the foreground each
    time an Android resource is saved, so this makes Visual Studio more
    responsive when working on Android resources.
```

### Template for external tool or library version update

  * Include a heading that starts with the tool or library name and ends with
    `version update` or `version update to $VERSION`.

  * Link to the upstream release notes if possible.

Examples:

```
### Mono.Data.Sqlite SQLite version update

The version of SQLite used by Mono.Data.Sqlite in Xamarin.Android has been
updated from 3.27.1 to [3.28.0](https://sqlite.org/releaselog/3_28_0.html),
bringing in several improvements and bug fixes.
```

```
### AAPT2 version update to 3.5.0-5435860

The version of the Android Asset Packaging Tool AAPT2 included in
Xamarin.Android has been updated from 3.4.1-5326820 to 3.5.0-5435860.
```

### Template for new feature or behavior

  * Describe what the change is, why it was added, and how to enable it or
    account for it.

  * Include a descriptive heading for the change.  The heading will also appear
    in the "what's new" section at the top of the release notes.

  * List any known issues under a `#### Known issues` subheading.

Example:

````
### XA0119 warning for non-ideal Debug build configurations

Xamarin.Android 10.1 adds a new XA0119 build warning to identify cases where
projects might have unintentionally enabled settings that slow down deployments
in the Debug configuration.  For the best Debug configuration deployment
performance, first ensure that **Use Shared Runtime** is enabled in the Visual
Studio project property pages.  This sets the `$(AndroidUseSharedRuntime)`
MSBuild property to `true` in your *.csproj* file:

```xml
<PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
  <AndroidUseSharedRuntime>true</AndroidUseSharedRuntime>
</PropertyGroup>
```

When **Use Shared Runtime** is enabled, an XA0119 warning will appear if any of
the following settings are enabled:

  * **Android Package Format** set to **bundle**
  * **Code shrinker**
  * **AOT Compilation**
  * **Enable Startup Tracing**
  * **Linking**

Be sure to disable these settings for the best Debug configuration deployment
performance.
````

### Template for other issue fix or small improvement

  * Use past tense verbs to describe the old problematic behavior.

  * List all public GitHub or Developer Community items fixed by the change,
    excluding items closed as duplicates.  If no public items were fixed, or if
    the scope of the fix was larger than any of the individual items, you can
    skip this step, or if the PR already exists, you can list that instead.

  * Where possible and appropriate, include the error text that users would have
    seen before the fix.

  * For items based on internal findings rather than end-user reports, aim to
    include information about how end-user projects could have in theory
    encountered the behavior.

  * If the fix is for a regression and you know when the problem started,
    include that version info.

  * Use one of the following headings:

      * `#### Application and library build and deployment`
      * `#### Application behavior on device and emulator`
      * `#### Android API bindings`
      * `#### Design-time build process`
      * `#### Xamarin.Android SDK installation`
      * `#### Application Mono Framework behavior on device and emulator`

Examples:

```
#### Application behavior on device and emulator

  * [Developer Community 743965](https://developercommunity.visualstudio.com/content/problem/743965/newtonsoftjsonjsonreaderexception-unexpected-chara.html),
    [GitHub 3626](https://github.com/xamarin/xamarin-android/issues/3626):
    Starting in Xamarin.Android 10.0, *Newtonsoft.Json.JsonReaderException:
    Unexpected character encountered* caused `JsonConvert.DeserializeObject()`
    to fail in apps built in the Release configuration.
```

```
#### Application behavior on device and emulator

  * [GitHub 3498](https://github.com/xamarin/xamarin-android/issues/3498):
    Writing to the `System.IO.Stream` returned by
    `Application.Context.ContentResolver.OpenOutputStream()` completed without
    error but did not write any bytes to the output location.
```

```
#### Application and library build and deployment

  * [GitHub PR 3685](https://github.com/xamarin/xamarin-android/pull/3685):
    *warning ANDJS0000* was always shown for the `jarsigner` warnings *The
    signer's certificate is self-signed.* and *No -tsa or -tsacert is provided
    and this jar is not timestamped.*  Those messages were not relevant for
    Xamarin.Android users, so Xamarin.Android 10.1 now reports them as
    informational messages rather than warnings.
```

### Template for submodule or `.external` bump

  * Use a single file for all the notes.

  * Use section headings to help organize the notes.

  * For items that do not have a public issue or PR on GitHub or Developer
    Community, don't worry about including a link.

  * For Mono Framework version updates, the main focus for now is to include
    entries for any issues in the xamarin-android repo fixed by the bump.

Example showing multiple release notes sections for a Java.Interop bump:

```
### Build and deployment performance

  * [Java.Interop GitHub PR 440](https://github.com/xamarin/java.interop/pull/440),
    [Java.Interop GitHub PR 441](https://github.com/xamarin/java.interop/pull/441),
    [Java.Interop GitHub PR 442](https://github.com/xamarin/java.interop/pull/442),
    [Java.Interop GitHub PR 448](https://github.com/xamarin/java.interop/pull/448),
    [Java.Interop GitHub PR 449](https://github.com/xamarin/java.interop/pull/449),
    [Java.Interop GitHub PR 452](https://github.com/xamarin/java.interop/pull/452):
    Optimize several of the build steps for bindings projects.  For a large
    binding like *Mono.Android.dll* itself, this reduced the total build time in
    a test environment by about 50 seconds.

#### Application and library build and deployment

  * [Java.Interop GitHub PR 458](https://github.com/xamarin/java.interop/pull/458):
    Bindings projects did not yet automatically generate event handlers for Java
    listener interfaces where the *add* or *set* method of the interface took
    two arguments instead of just one.
```

## Other guidelines

Feel free to include images in the notes if appropriate.  The image files can be
added to `Documentation/release-notes/images/`.

The draft notes don't need to follow the [xamarin-docs contribution
guidelines][docs-guidelines].  For example, it's fine to use backticks around
paths in these notes rather than italics.  That makes it easier to avoid
unintentional formatting issues when paths include characters like `_` or `*`.

Similarly, you can use any list indentation or line wrapping style you like.

[docs-guidelines]: https://github.com/MicrosoftDocs/xamarin-docs/blob/live/contributing-guidelines/template.md

## Publication workflow

(To be added.)
