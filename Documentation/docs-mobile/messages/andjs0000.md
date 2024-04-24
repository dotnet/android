---
title: .NET for Android error/warning ANDJS0000
description: ANDJS0000 error/warning code
ms.date: 04/11/2024
---
# .NET for Android error/warning ANDJS0000

## Issue

This message indicates that the Java `jarsigner` command line tool used by
.NET for Android reported an error or warning.

Errors reported by `jarsigner` and other Android command line tooling are
outside of .NET for Android's control, so a general error code of
ANDJS0000 is used reporting the exact message.

## Solution

To learn more about `jarsigner` and its usage, see the Java documentation
[here][jarsigner].

[jarsigner]: https://docs.oracle.com/javase/7/docs/technotes/tools/windows/jarsigner.html
