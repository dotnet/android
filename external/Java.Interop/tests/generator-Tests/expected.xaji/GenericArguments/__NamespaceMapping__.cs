using System;

[assembly:global::Android.Runtime.NamespaceMapping (Java = "java.lang", Managed="Java.Lang")]
[assembly:global::Android.Runtime.NamespaceMapping (Java = "com.google.android.exoplayer.drm", Managed="Com.Google.Android.Exoplayer.Drm")]

[global::System.Runtime.InteropServices.UnmanagedFunctionPointer (global::System.Runtime.InteropServices.CallingConvention.Winapi)]
delegate sbyte _JniMarshal_PPL_B (IntPtr jnienv, IntPtr klass, IntPtr p0);
[global::System.Runtime.InteropServices.UnmanagedFunctionPointer (global::System.Runtime.InteropServices.CallingConvention.Winapi)]
delegate void _JniMarshal_PPL_V (IntPtr jnienv, IntPtr klass, IntPtr p0);
[global::System.Runtime.InteropServices.UnmanagedFunctionPointer (global::System.Runtime.InteropServices.CallingConvention.Winapi)]
delegate void _JniMarshal_PPLLIIL_V (IntPtr jnienv, IntPtr klass, IntPtr p0, IntPtr p1, int p2, int p3, IntPtr p4);

