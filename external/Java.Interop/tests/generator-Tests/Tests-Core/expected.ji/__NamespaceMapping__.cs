using System;

[assembly:global::Android.Runtime.NamespaceMapping (Java = "android.view", Managed="Android.Views")]
[assembly:global::Android.Runtime.NamespaceMapping (Java = "android.text", Managed="Android.Text")]
[assembly:global::Android.Runtime.NamespaceMapping (Java = "java.lang", Managed="Java.Lang")]

delegate int _JniMarshal_PPL_I (IntPtr jnienv, IntPtr klass, IntPtr p0);
delegate void _JniMarshal_PPL_V (IntPtr jnienv, IntPtr klass, IntPtr p0);
