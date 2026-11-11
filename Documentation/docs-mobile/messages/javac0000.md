---
title: .NET for Android error JAVAC0000
description: JAVAC0000 error code
ms.date: 04/11/2024
f1_keywords:
  - "JAVAC0000"
---

# .NET for Android error JAVAC0000

## Example messages

```
Foo.java : error JAVAC0000: Foo.java:1: error: class, interface, or enum expected
```

```
Foo.java : error JAVAC0000: Foo.java:1: error: ';' expected
```

## Issue

During a normal .NET for Android build, Java source code is generated
and compiled. This message indicates that [`javac`][javac], the Java
programming language compiler, failed to compile Java source code.

The `_CompileJava` and `_CompileBindingJava` targets invoke `javac` through
`Exec` using a UTF-8 response file. Compiler errors retain their source file
and `JAVAC0000` code, but the line number is part of the message rather than
a structured MSBuild location. Caret-derived columns, multiline error
grouping, and library-project assembly-name annotations are no longer added.
The original compiler output, including source lines and carets, remains in
the build log. Out-of-memory failures still use the localized `XA5213`
diagnostic.

## Solution

If you have Java source code in your project with a build action of
`AndroidJavaSource`, verify your Java syntax is correct.

Consider submitting a [bug][bug] if you are getting this error under
normal circumstances.

[javac]: https://docs.oracle.com/javase/8/docs/technotes/tools/windows/javac.html
[bug]: https://github.com/dotnet/android/wiki/Submitting-Bugs,-Feature-Requests,-and-Pull-Requests
