using System;

[assembly:global::Android.Runtime.NamespaceMapping (Java = "java.lang", Managed="Java.Lang")]
[assembly:global::Android.Runtime.NamespaceMapping (Java = "java.util", Managed="Java.Util")]
[assembly:global::Android.Runtime.NamespaceMapping (Java = "test.me", Managed="Test.ME")]
[assembly:global::Android.Runtime.NamespaceMapping (Java = "", Managed="")]

[global::System.Runtime.InteropServices.UnmanagedFunctionPointer (global::System.Runtime.InteropServices.CallingConvention.Winapi)]
delegate IntPtr _JniMarshal_PP_L (IntPtr jnienv, IntPtr klass);
[global::System.Runtime.InteropServices.UnmanagedFunctionPointer (global::System.Runtime.InteropServices.CallingConvention.Winapi)]
delegate void _JniMarshal_PP_V (IntPtr jnienv, IntPtr klass);
[global::System.Runtime.InteropServices.UnmanagedFunctionPointer (global::System.Runtime.InteropServices.CallingConvention.Winapi)]
delegate int _JniMarshal_PPL_I (IntPtr jnienv, IntPtr klass, IntPtr p0);
[global::System.Runtime.InteropServices.UnmanagedFunctionPointer (global::System.Runtime.InteropServices.CallingConvention.Winapi)]
delegate IntPtr _JniMarshal_PPL_L (IntPtr jnienv, IntPtr klass, IntPtr p0);
[global::System.Runtime.InteropServices.UnmanagedFunctionPointer (global::System.Runtime.InteropServices.CallingConvention.Winapi)]
delegate void _JniMarshal_PPL_V (IntPtr jnienv, IntPtr klass, IntPtr p0);
[global::System.Runtime.InteropServices.UnmanagedFunctionPointer (global::System.Runtime.InteropServices.CallingConvention.Winapi)]
delegate bool _JniMarshal_PPL_Z (IntPtr jnienv, IntPtr klass, IntPtr p0);
#if !NET
namespace System.Runtime.Versioning {
    [System.Diagnostics.Conditional("NEVER")]
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Constructor | AttributeTargets.Enum | AttributeTargets.Event | AttributeTargets.Field | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Module | AttributeTargets.Property | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    internal sealed class SupportedOSPlatformAttribute : Attribute {
        public SupportedOSPlatformAttribute (string platformName) { }
    }
}
#endif

