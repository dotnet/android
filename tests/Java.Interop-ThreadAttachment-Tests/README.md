# JNI thread attachment host checks

The managed tests exercise attachment ownership and failure handling against a fake JavaVM:

```sh
dotnet test tests/Java.Interop-ThreadAttachment-Tests/Java.Interop-ThreadAttachment-Tests.csproj
```

The native probe exercises the real pthread destructor against a fake JavaVM. Set `NDK_INCLUDE` to the Android NDK toolchain's `sysroot/usr/include` directory, then run from the repository root on macOS or Linux:

```sh
mkdir -p tests/Java.Interop-ThreadAttachment-Tests/obj
clang++ -std=c++23 -D_JAVASOFT_JNI_H_ -include "$NDK_INCLUDE/jni.h" \
  -I src/native/common/include -I src/native/clr/include -idirafter "$NDK_INCLUDE" \
  src/native/clr/host/runtime-environment.cc \
  tests/Java.Interop-ThreadAttachment-Tests/runtime-environment-probe.cc \
  -pthread -o tests/Java.Interop-ThreadAttachment-Tests/obj/runtime-environment-probe
tests/Java.Interop-ThreadAttachment-Tests/obj/runtime-environment-probe
```

These host checks do not replace the `JniThreadAttachment` on-device tests for CoreCLR and NativeAOT.
