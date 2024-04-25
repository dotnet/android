---
title: .NET for Android error/warning ANDZA0000
description: ANDZA0000 error/warning code
ms.date: 04/11/2024
---
# .NET for Android error/warning ANDZA0000

## Issue

This message indicates that the Android `zipalign` command line tool used by
.NET for Android reported an error or warning.

Errors reported by `zipalign` and other Android command line tooling are
outside of .NET for Android's control, so a general error code of
ANDZA0000 is used reporting the exact message.

## Solution

To learn more about `zipalign` and its usage, see the Android documentation
[here][zipalign].

[zipalign]: https://developer.android.com/studio/command-line/zipalign
