using System;

[assembly:global::Android.Runtime.NamespaceMapping (Java = "java.lang", Managed="Java.Lang")]
[assembly:global::Android.Runtime.NamespaceMapping (Java = "xamarin.test", Managed="Xamarin.Test")]

delegate void _JniMarshal_PP_V (IntPtr jnienv, IntPtr klass);
