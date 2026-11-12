# Access Java file stream channels

`AssetFileDescriptor.CreateInputStream()` and `CreateOutputStream()` return
`System.IO.Stream`. To access the Java stream's `Channel` property, use
[`InputStreamInvoker.BaseInputStream`][input] or
[`OutputStreamInvoker.BaseOutputStream`][output], then
`JavaCast<Java.IO.FileInputStream>()` or `JavaCast<Java.IO.FileOutputStream>()`.
No reflection or new stream from the raw file descriptor is needed.

For two open `AssetFileDescriptor` objects, `inputDescriptor` and
`outputDescriptor`:

```csharp
using System;
using Android.Runtime;

using var input = inputDescriptor.CreateInputStream ();
using var output = outputDescriptor.CreateOutputStream ();

if (input is not InputStreamInvoker inputInvoker)
	throw new InvalidOperationException ("Expected a Java input stream.");
if (output is not OutputStreamInvoker outputInvoker)
	throw new InvalidOperationException ("Expected a Java output stream.");

var inputChannel = inputInvoker.BaseInputStream
	.JavaCast<Java.IO.FileInputStream> ().Channel;
var outputChannel = outputInvoker.BaseOutputStream
	.JavaCast<Java.IO.FileOutputStream> ().Channel;
```

`JavaCast<T>()` returns a managed wrapper of the requested type for the same
Java object. It does not open a second file stream.

Keep the .NET streams open while using the channels. Disposing a .NET stream
calls `Close()` on its original Java stream and disposes that stream's managed
wrapper.
Android's auto-close streams close the associated `ParcelFileDescriptor`.
Do not separately close or dispose the borrowed Java streams or channels while
the .NET streams are in use. Call each creation method only once per descriptor.

Channel access does not guarantee a zero-copy transfer or make every content
URI seekable. A provider can return a pipe, or an asset can cover only part of
a file. Check the descriptor's
`StartOffset` and `DeclaredLength`, and the Android version's channel behavior,
before using channel positions or transfer methods. Do not assume that channel
operations enforce the same asset bounds as stream reads and writes.
`FileChannel.TransferTo()` can transfer fewer bytes than requested, including
zero; callers must handle partial transfers and lack of progress.

See the [Android `AssetFileDescriptor` reference][descriptor] for the auto-close
stream contract and the [Android `FileChannel` reference][channel] for transfer
and positioning rules.

[input]: https://learn.microsoft.com/dotnet/api/android.runtime.inputstreaminvoker.baseinputstream
[output]: https://learn.microsoft.com/dotnet/api/android.runtime.outputstreaminvoker.baseoutputstream
[descriptor]: https://developer.android.com/reference/android/content/res/AssetFileDescriptor
[channel]: https://developer.android.com/reference/java/nio/channels/FileChannel
