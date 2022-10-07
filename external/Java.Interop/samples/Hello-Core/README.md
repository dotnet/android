# Hello-Core

Use as little of `Java.Interop.dll` as possible.  No object mapping.

Usage:

```
Options:
      --jvm=PATH             PATH to JVM to use.
      --cp, --classpath=JAR-OR-DIRECTORY
                             Add JAR-OR-DIRECTORY to JVM classpath.
  -J=VALUE                   Pass the specified option to the JVM.
  -h, --help                 Show this message and exit.
```

`-J` can be used to add runtime options to the JVM instance, e.g.

```shell
# Enable verbose JNI logging from the JVM
% dotnet run -- --jvm /Library/Java/JavaVirtualMachines/microsoft-11.jdk/Contents/Home/lib/jli/libjli.dylib  -J-verbose:jni
[Dynamic-linking native method java.lang.Object.registerNatives ... JNI]
[Registering JNI native method java.lang.Object.hashCode]
[Registering JNI native method java.lang.Object.wait]
…
```

The sample will create a `java.lang.Object` instance and invoke `Object.toString()` on it.

```
% dotnet run -- --jvm /Library/Java/JavaVirtualMachines/microsoft-11.jdk/Contents/Home/lib/jli/libjli.dylib
Object_class=0x7ff04f105b98/L
Object_val=0x7ff04f105ba8/L
Object_val.toString()=java.lang.Object@5cbc508c
Object_val.toString()=java.lang.Object@3419866c
```
