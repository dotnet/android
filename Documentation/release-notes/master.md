# Xamarin.Android 10.0.99 (master) draft release notes

## What's new in Xamarin.Android 10.0.99

### Summary of what's new in Xamarin.Android 10.0.99

  * [Template for new features](#template-for-new-features)
  * [Build and deployment performance](#build-and-deployment-performance)
  * [App startup performance](#app-startup-performance)
  * [Java exception wrapping](#java-exception-wrapping)
  * [Issues fixed](#issues-fixed)

### Template for new features

Description of new feature

#### Known issues

  * Description of known issue in the new feature, if applicable

### Build and deployment performance

  * When **Use Fast Deployment** is enabled, cache some of the information
    that is retrieved from the device during the first deployment so that it can
    be reused on subsequent deployments of the same app until Visual Studio is
    restarted.  This reduced the time for the `InstallPackageAssemblies` task
    during the second deployment of Xamarin.Forms app where one line of a XAML
    file was changed from about 500 milliseconds to about 375 milliseconds in a
    test environment.

### Static/Default Interface Methods (Preview)

With the new support for default interface methods in C# 8.0 we can now produce bindings that better match Java libraries 
that use these features.  This includes:

* Default interface methods
* Static interface methods
* Interface constants

To enable this preview in your bindings project, add the following properties to your `.csproj`:

```
<LangVersion>preview</LangVersion>
<_EnableInterfaceMembers>True</_EnableInterfaceMembers>
```

Note that enabling this only adds new members, it does not remove the existing alternatives previously used to expose
these methods/fields.


### Build and deployment performance

  * Bindings projects should now build considerably faster:
    * [GitHub PR 440](https://github.com/xamarin/java.interop/pull/440)
    * [GitHub PR 441](https://github.com/xamarin/java.interop/pull/441)
    * [GitHub PR 442](https://github.com/xamarin/java.interop/pull/442)
    * [GitHub PR 448](https://github.com/xamarin/java.interop/pull/448)
    * [GitHub PR 449](https://github.com/xamarin/java.interop/pull/449)
    * [GitHub PR 452](https://github.com/xamarin/java.interop/pull/452)
  * [GitHub PR nnnn](https://github.com/xamarin/xamarin-android/pull/nnnn):
    Description of improvement

### App startup performance

### Java exception wrapping

`Mono.Android.dll` has been audited to find code which can throw Java exceptions in context where throwing a .NET exception
would be more logical/expected. `Xamarin.Android`'s has always thrown the Java exceptions, however it poses a problem for
applications which have most of their code stored in portable assemblies that do not reference `Mono.Android` as they are unable
to catch and handle the Java exceptions. The simplest solution - to always wrap Java exceptions in BCL ones - is unfortunately
not viable because of backward compatibility. For this release the old behavior remains the default, but one can opt in to the
new wrapping behavior by setting the `$(AndroidBoundExceptionType)` MSBuild property to `System`. After this is done, all the
**documented** Java exceptions will be wrapped in corresponding BCL exceptions (with the original exception placed in the
`InnerException` property and the original message reused in the wrapper exception).
Note that this wrapping happens **only** for Java/Android types which use the **.NET** classes (e.g. wherever Java.IO.InputStream
is used by the Android class but `Xamarin.Android` binds it as `System.IO.Stream`) 

### Issues fixed

  * [GitHub NNNN](https://github.com/xamarin/xamarin-android/issues/NNNN):
    Description of issue fixed
  * [Developer Community NNNNNN](https://developercommunity.visualstudio.com/content/problem/NNNNNN/title-of-issue-fixed.html),
    [GitHub NNNN](https://github.com/xamarin/xamarin-android/issues/NNNN):
    Description of issue fixed that has both a Developer Community item and a corresponding GitHub issue

## Known issues

## Feedback

Your feedback is important to us.  If there are any problems with this release, check our [GitHub Issues](https://github.com/xamarin/xamarin-android/issues), [Xamarin.Android Community Forums](https://forums.xamarin.com/categories/android) and [Visual Studio Developer Community](https://developercommunity.visualstudio.com/) for existing issues.  For new issues within the Xamarin.Android SDK, please report a [GitHub Issue](https://github.com/xamarin/xamarin-android/issues/new).  For general Xamarin.Android experience issues, let us know via the [Report a Problem](https://docs.microsoft.com/visualstudio/ide/how-to-report-a-problem-with-visual-studio) option found in your favorite IDE under **Help &gt; Report a Problem**.

## Contributors

A big ***Thank You!*** to contributors who made improvements in this release:

## OSS core

Xamarin.Android 10.0.99 is based on the open-source Xamarin.Android repositories:

  * Core JNI interaction logic is in the [Java.Interop](https://github.com/xamarin/Java.Interop/tree/master) repo.
  * Android bindings and MSBuild tooling are in the [xamarin-android](https://github.com/xamarin/xamarin-android/tree/master) repo.
  * Chat is in the [`xamarin/xamarin-android` Gitter channel](https://gitter.im/xamarin/xamarin-android).
