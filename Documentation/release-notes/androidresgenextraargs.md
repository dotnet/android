#### Application and library build and deployment

A text templating feature for aapt/aapt2 has been removed that
provided a workaround for the Amazon Fire Phone:

```xml
<AndroidResgenExtraArgs>-I ${library.imports:eac-api.jar} -I ${library.imports:euclid-api.jar}</AndroidResgenExtraArgs>
```

The `${library.imports:...}` syntax should no longer be needed by
modern Android libraries. [`.aar`][aar] files are the recommended way
for Java/Kotlin libraries to distribute Android resources to be
consumed by Xamarin.Android application projects.

Note that the `$(AndroidResgenExtraArgs)` and
`$(AndroidAapt2LinkExtraArgs)` MSBuild properties will continue to
pass additional arguments to `aapt` and `aapt2 link` with the
`${library.imports:...}` syntax removed.

[aar]: https://developer.android.com/studio/projects/android-library#aar-contents
