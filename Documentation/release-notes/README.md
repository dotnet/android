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

### Build time or app performance improvement

  * Use a command form verb in the first sentence, as if you were telling
    someone to do something.
  * Include a before and after time comparsion if possible.

Examples:

```
  * [GitHub PR 3640](https://github.com/xamarin/xamarin-android/pull/3640):
    Use System.Reflection.Metadata rather than Cecil for
    `ResolveLibraryProjectImports`.  This reduced the time for the
    `ResolveLibraryProjectImports` task from about 4.8 seconds to about 4.5
    seconds for a small test Xamarin.Forms app on an initial clean build.

  * [GitHub PR 3729](https://github.com/xamarin/xamarin-android/pull/3729):
    Initialize application logging and uncaught exception handling lazily.  This
    reduced the time to display the first screen of a small test Xamarin.Forms
    app from about 783 milliseconds to about 754 milliseconds for a Release
    configuration build on a Google Pixel 3 XL device.
```

### New feature or behavior

  * Describe what the change is, why it was added, and how to enable it or
    account for it.

Example:

````
#### XA0119 warning for non-ideal Debug build configurations

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

### External tool or library version update

Example:

```
#### Mono.Data.Sqlite SQLite version update

The version of SQLite used by Mono.Data.Sqlite in Xamarin.Android has been
updated from 3.27.1 to [3.28.0](https://sqlite.org/releaselog/3_28_0.html),
bringing in several improvements and bug fixes.
```

### Other issue fix or small improvement

  * Use past tense verbs when describing the previous problematic behavior.
  * List all public GitHub or Developer Community items that were fixed by the
    change.  If no public items were fixed, or if the scope of the fix was
    larger than any of the individual items, list the PR instead.
  * Where possible and appropriate, include the error text that users would have
    seen before the fix.
  * If the fix is for a regression and you know when the problem started,
    include that version info.

Examples:

```
  * [Developer Community 743965](https://developercommunity.visualstudio.com/content/problem/743965/newtonsoftjsonjsonreaderexception-unexpected-chara.html),
    [GitHub 3626](https://github.com/xamarin/xamarin-android/issues/3626):
    Starting in Xamarin.Android 10.0, *Newtonsoft.Json.JsonReaderException:
    Unexpected character encountered* caused `JsonConvert.DeserializeObject()`
    to fail in apps built in the Release configuration.

  * [GitHub 3498](https://github.com/xamarin/xamarin-android/issues/3498):
    Writing to the `System.IO.Stream` returned by
    `Application.Context.ContentResolver.OpenOutputStream()` completed without
    error but did not write any bytes to the output location.

  * [GitHub PR 3561](https://github.com/xamarin/xamarin-android/pull/3561):
    Starting in Xamarin.Android 10.0, the `_GenerateJavaStubs` target could run
    during builds where it wasn't needed if any of the previous builds had
    involved a change to a class that inherited from `Java.Lang.Object`.

  * [GitHub PR 3685](https://github.com/xamarin/xamarin-android/pull/3685):
    *warning ANDJS0000* was always shown for the `jarsigner` warnings *The
    signer's certificate is self-signed.* and *No -tsa or -tsacert is provided
    and this jar is not timestamped.*  Those messages were not relevant for
    Xamarin.Android users, so Xamarin.Android 10.1 now reports them as
    informational messages rather than warnings.
```

### Submodule or `.external` bump

  * Use a single file for all of the notes for the bump.
  * Use section headings to help organize the notes.
  * For items that do not have a public issue or PR on GitHub or Developer
    Community, use the xamarin-android bump PR as the item link instead.
  * For Mono Framework version updates, the main focus for now is to include an
    entry for every issue in the xamarin-android repo that is fixed by the bump.

Example showing mutiple release notes sections for a Java.Interop bump:

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

### Issues fixed

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

Similarly, you can use any list indentation or line wrapping style that you
like.

[docs-guidelines]: https://github.com/MicrosoftDocs/xamarin-docs/blob/live/contributing-guidelines/template.md

## Publication workflow

(To be added.)
