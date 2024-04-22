---
title: .NET for Android error JAVAC0000
description: JAVAC0000 error code
ms.date: 04/11/2024
---
# .NET for Android error JAVAC0000

## Example messages

```
error JAVAC0000: Foo.java(1,8): javac error: class, interface, or enum expected
```

```
error JAVAC0000: Foo.java(1,41): javac error: ';' expected
```

## Issue

During a normal .NET for Android build, Java source code is generated
and compiled. This message indicates that [`javac`][javac], the Java
programming language compiler, failed to compile Java source code.

## Solution

If you have Java source code in your project with a build action of
`AndroidJavaSource`, verify your Java syntax is correct.

Consider submitting a [bug][bug] if you are getting this error under
normal circumstances.

[javac]: https://docs.oracle.com/javase/8/docs/technotes/tools/windows/javac.html
[bug]: https://github.com/xamarin/xamarin-android/wiki/Submitting-Bugs,-Feature-Requests,-and-Pull-Requests
