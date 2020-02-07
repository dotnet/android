**apkdiff** is a tool to compare Android packages

```
Usage: apkdiff.exe OPTIONS* <package1.apk> <package2.apk>

Compares APK packages content or APK package with content description

Copyright 2020 Microsoft Corporation

Options:
  -c, --comment=VALUE        Comment to be saved inside .apkdesc file
  -h, --help, -?             Show this message and exit
  -s, --save-descriptions    Save .apkdesc files next to the apk package(s)
  -v, --verbose              Output information about progress during the run
                               of the tool
```

It can be use to compare Android packages (apk's) and/or apk
descriptions files (apkdesc)

### Example usage

```
mono apkdiff.exe xa-d16-4/bin/TestRelease/Xamarin.Forms_Performance_Integration.apkdesc xa-d16-5/bin/TestRelease/Xamarin.Forms_Performance_Integration.apk
Size difference in bytes ([*1] apk1 only, [*2] apk2 only):
  +       49184 lib/armeabi-v7a/libmonosgen-2.0.so
  +       13824 assemblies/Mono.Android.dll
  +       10824 lib/x86/libmonodroid.so
  +        5604 lib/armeabi-v7a/libmonodroid.so
  +        1864 lib/armeabi-v7a/libxamarin-app.so
  +        1864 lib/x86/libxamarin-app.so
  +         168 classes.dex
  -        3584 assemblies/System.dll
  -       10240 assemblies/mscorlib.dll
  -       71680 assemblies/Mono.Security.dll
  -       77792 lib/x86/libmonosgen-2.0.so
Summary:
  -       46984 Package size difference
```
