### Other warning and error changes

#### Updated XA0113 warning for Google Play submission requirements

The XA0113 warning has been updated to reflect [a more recent minimum target
version of Android 10 (API level 29)][targetsdk] for submissions to the Google
Play store.  The following warning will now appear for projects that have an
earlier version set under **Compile using Android version: (Target Framework)**
in the Visual Studio project property pages:

```
warning XA0113: Google Play requires that new applications and updates must use a TargetFrameworkVersion of v10.0 (API level 29) or above. You are currently targeting v9.0 (API level 28).
```

[targetsdk]: https://support.google.com/googleplay/android-developer/answer/9859152?#targetsdk
