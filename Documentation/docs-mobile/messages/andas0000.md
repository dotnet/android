---
title: .NET for Android error/warning ANDAS0000
description: ANDAS0000 error/warning code
ms.date: 04/11/2024
---
# .NET for Android error/warning ANDAS0000

## Issue

This message indicates that the Android `apksigner` command line tool used by
.NET for Android reported an error or warning.

Errors reported by `apksigner` and other Android command line tooling are
outside of .NET for Android's control, so a general error code of
ANDAS0000 is used reporting the exact message.

## Solution

To learn more about `apksigner` and its usage, see the Android documentation
[here][apksigner].

[apksigner]: https://developer.android.com/studio/command-line/apksigner
