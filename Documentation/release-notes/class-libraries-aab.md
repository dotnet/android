#### Application and library build and deployment

* [GitHub Issue 5024](https://github.com/xamarin/xamarin-android/issues/5024):
  *error XA0119: Using the shared runtime and Android App Bundles at
  the same time is not currently supported* build error could
  mistakenly appear for Xamarin.Android class libraries when
  `$(AndroidPackageFormat)` is set to `aab`.
