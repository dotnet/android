using System;

[assembly:global::Android.Runtime.NamespaceMapping (Java = "java.lang", Managed="Java.Lang")]
[assembly:global::Android.Runtime.NamespaceMapping (Java = "xamarin.test", Managed="Xamarin.Test")]

delegate IntPtr _JniMarshal_PPI_L (IntPtr jnienv, IntPtr klass, int p0);
