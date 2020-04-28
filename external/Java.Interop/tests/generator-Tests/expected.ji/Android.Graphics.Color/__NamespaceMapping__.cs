using System;

[assembly:global::Android.Runtime.NamespaceMapping (Java = "java.lang", Managed="Java.Lang")]
[assembly:global::Android.Runtime.NamespaceMapping (Java = "xamarin.test", Managed="Xamarin.Test")]

delegate int _JniMarshal_PP_I (IntPtr jnienv, IntPtr klass);
delegate void _JniMarshal_PPI_V (IntPtr jnienv, IntPtr klass, int p0);
