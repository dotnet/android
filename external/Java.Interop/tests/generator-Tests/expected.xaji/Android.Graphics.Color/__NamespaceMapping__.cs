using System;

[assembly:global::Android.Runtime.NamespaceMapping (Java = "java.lang", Managed="Java.Lang")]
[assembly:global::Android.Runtime.NamespaceMapping (Java = "xamarin.test", Managed="Xamarin.Test")]

[global::System.Runtime.InteropServices.UnmanagedFunctionPointer (global::System.Runtime.InteropServices.CallingConvention.Winapi)]
delegate int _JniMarshal_PP_I (IntPtr jnienv, IntPtr klass);
[global::System.Runtime.InteropServices.UnmanagedFunctionPointer (global::System.Runtime.InteropServices.CallingConvention.Winapi)]
delegate void _JniMarshal_PPI_V (IntPtr jnienv, IntPtr klass, int p0);
#if !NET
namespace System.Runtime.Versioning {
    [System.Diagnostics.Conditional("NEVER")]
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Constructor | AttributeTargets.Enum | AttributeTargets.Event | AttributeTargets.Field | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Module | AttributeTargets.Property | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    internal sealed class SupportedOSPlatformAttribute : Attribute {
        public SupportedOSPlatformAttribute (string platformName) { }
    }
}
#endif

