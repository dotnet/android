**apkdiff** is a tool to compare Android packages

Upstream repository: https://github.com/radekdoulik/apkdiff
Location of the tool included in Xamarin.Android: https://github.com/xamarin/xamarin-android/tree/master/tools/apkdiff

```
Usage: apkdiff.exe OPTIONS* <package1.[apk|aab][desc]> [<package2.[apk|aab][desc]>]

Compares APK/AAB packages content or APK/AAB package with content description

Copyright 2020 Microsoft Corporation

Options:
  -c, --comment=VALUE        Comment to be saved inside description file
  -h, --help, -?             Show this message and exit
      --test-apk-size-regression=BYTES
                             Check whether apk size increased more than BYTES
      --test-assembly-size-regression=BYTES
                             Check whether any assembly size increased more
                               than BYTES
  -s, --save-descriptions    Save .[apk|aab]desc description files next to the
                               package(s) or to the specified path
      --save-description-1=PATH
                             Save .[apk|aab]desc description for first package
                               to PATH
      --save-description-2=PATH
                             Save .[apk|aab]desc description for second package
                               to PATH
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

### Example output with shared libraries and assemblies details
```
mono apkdiff.exe Xamarin.Forms_Performance_Integration-Signed-NewNDK-Default.apk Xamarin.Forms_Performance_Integration-Signed-NewNTR-Default.apk 
Size difference in bytes ([*1] apk1 only, [*2] apk2 only):
  +       42496 assemblies/Mono.Android.dll
    +             Type Java.Nio.ByteBuffer
    +             Type Java.Nio.ByteBuffer/__<$>_jni_marshal_methods
    +             Type Java.Nio.ByteBufferInvoker
    +             Type Java.Nio.Channels.FileChannel
    +             Type Java.Nio.Channels.FileChannel/__<$>_jni_marshal_methods
    +             Type Java.Nio.Channels.FileChannelInvoker
    +             Type Java.Nio.Channels.FileChannelInvoker/__<$>_jni_marshal_methods
    +             Type Java.Nio.Channels.IByteChannel
    +             Type Java.Nio.Channels.IByteChannelInvoker
    +             Type Java.Nio.Channels.IByteChannelInvoker/__<$>_jni_marshal_methods
    +             Type Java.Nio.Channels.IChannel
    +             Type Java.Nio.Channels.IChannelInvoker
    +             Type Java.Nio.Channels.IChannelInvoker/__<$>_jni_marshal_methods
    +             Type Java.Nio.Channels.IGatheringByteChannel
    +             Type Java.Nio.Channels.IGatheringByteChannelInvoker
    +             Type Java.Nio.Channels.IGatheringByteChannelInvoker/__<$>_jni_marshal_methods
    +             Type Java.Nio.Channels.IInterruptibleChannel
    +             Type Java.Nio.Channels.IInterruptibleChannelInvoker
    +             Type Java.Nio.Channels.IInterruptibleChannelInvoker/__<$>_jni_marshal_methods
    +             Type Java.Nio.Channels.IReadableByteChannel
    +             Type Java.Nio.Channels.IReadableByteChannelInvoker
    +             Type Java.Nio.Channels.IReadableByteChannelInvoker/__<$>_jni_marshal_methods
    +             Type Java.Nio.Channels.IScatteringByteChannel
    +             Type Java.Nio.Channels.IScatteringByteChannelInvoker
    +             Type Java.Nio.Channels.IScatteringByteChannelInvoker/__<$>_jni_marshal_methods
    +             Type Java.Nio.Channels.ISeekableByteChannel
    +             Type Java.Nio.Channels.ISeekableByteChannelInvoker
    +             Type Java.Nio.Channels.ISeekableByteChannelInvoker/__<$>_jni_marshal_methods
    +             Type Java.Nio.Channels.IWritableByteChannel
    +             Type Java.Nio.Channels.IWritableByteChannelInvoker
    +             Type Java.Nio.Channels.IWritableByteChannelInvoker/__<$>_jni_marshal_methods
    +             Type Java.Nio.Channels.Spi.AbstractInterruptibleChannel
    +             Type Java.Nio.Channels.Spi.AbstractInterruptibleChannel/__<$>_jni_marshal_methods
    +             Type Java.Nio.Channels.Spi.AbstractInterruptibleChannelInvoker
    +             Type Java.IO.FileInputStream
    +             Type Java.IO.FileInputStream/__<$>_jni_marshal_methods
  +        6264 lib/armeabi-v7a/libxamarin-app.so
                  Symbol size difference
    +        3480 mj_typemap
    +        2784 jm_typemap
  +        6264 lib/x86/libxamarin-app.so
                  Symbol size difference
    +        3480 mj_typemap
    +        2784 jm_typemap
  +        3584 assemblies/Java.Interop.dll
    -             Type Java.Interop.JniRuntime/JniValueManager/<>c__DisplayClass38_0
    -             Type Java.Interop.JniRuntime/JniTypeManager/<CreateGetTypesEnumerator>d__18
    -             Type Java.Interop.JniRuntime/JniTypeManager/<CreateGetTypesForSimpleReferenceEnumerator>d__20
    +             Type Microsoft.CodeAnalysis.EmbeddedAttribute
    +             Type System.Runtime.CompilerServices.NullableAttribute
    +             Type System.Diagnostics.CodeAnalysis.AllowNullAttribute
    +             Type System.Diagnostics.CodeAnalysis.MaybeNullAttribute
    +             Type System.Diagnostics.CodeAnalysis.NotNullAttribute
    +             Type System.Diagnostics.CodeAnalysis.NotNullWhenAttribute
    +             Type System.Diagnostics.CodeAnalysis.NotNullIfNotNullAttribute
    +             Type Java.Interop.JniRuntime/JniValueManager/<>c__DisplayClass37_0
    +             Type Java.Interop.JniRuntime/JniTypeManager/<CreateGetTypesEnumerator>d__17
    +             Type Java.Interop.JniRuntime/JniTypeManager/<CreateGetTypesForSimpleReferenceEnumerator>d__19
  -         512 assemblies/mscorlib.dll
Summary:
  +       45056 Package size difference
```
### Shared libraries section sizes comparison example
```
        Size difference in bytes ([*1] apk1 only, [*2] apk2 only):
          +      376724 lib/x86/libsqlite3_xamarin.so
                          Section size difference
            +      316967 .debug_loc
            +       60924 .debug_info
            +       15176 .debug_ranges
            +        4038 .debug_line
            +        1952 .debug_str
            +         127 .debug_abbrev
            +          44 .rodata
            +          40 .eh_frame_hdr
            +           8 .data.rel.ro
            -           4 .eh_frame
            -           4 .data
            -           8 .gnu.version
            -          12 .got.plt
            -          16 .gnu.hash
            -          16 .hash
            -          24 .rel.plt
            -          38 .dynstr
            -          48 .plt
            -          64 .dynsym
            -          84 .comment
            -       23984 .text
```
### Size regression test example
```
apkdiff.exe --test-apk-size-regression=51200
--test-assembly-size-regression=51200 Xamarin.Forms_Performance_Integration-Signed-Release.apkdesc TestDebug\Xamarin.Forms_Performance_Integration-Signed.apk
Size difference in bytes ([*1] apk1 only, [*2] apk2 only):
  +  26,749,952 assemblies/Mono.Android.dll
Error: apkdiff: Assembly size differs more than 51,200 bytes.
  +  14,655,424 assemblies/Mono.Android.pdb *2
  +   2,423,388 lib/x86/libmonosgen-2.0.so
  +   2,413,568 assemblies/mscorlib.dll
Error: apkdiff: Assembly size differs more than 51,200 bytes.
...
Summary:
  +  43,001,792 Assemblies 303.25% (of 14,180,480)
  +  10,653,224 Shared libraries 89.86% (of 11,855,444)
  +  66,973,060 Package size difference 318.80% (of 21,007,581)
Error: apkdiff: PackageSize differ more than 51,200 bytes. apk1 size: 21,007,581 bytes, apk2 size: 87,980,641 bytes.
Error: apkdiff: Size regression occured, 39 test(s) failed.
```
### Comparison including DEX file example
```
apkdiff Xamarin.Forms_Performance_Integration-Signed-orig.apk Xamarin.Forms_Performance_Integration-Signed.apk
Size difference in bytes ([*1] apk1 only, [*2] apk2 only):
  +     255,792 classes.dex
    +       2,023 strings count
    +         260 types count
    +         409 prototypes count
    +       1,100 fields count
    +       2,548 methods count
    +         237 classes count
    +     204,984 data section size
  +         512 assemblies/System.dll
  -          20 AndroidManifest.xml
Summary:
  +     255,792 Davik executables 13.15% (of 1,944,832)
  +         512 Assemblies 0.00% (of 14,180,480)
  +           0 Shared libraries 0.00% (of 11,856,152)
  +     106,496 Package size difference 0.51% (of 21,007,581)
```
