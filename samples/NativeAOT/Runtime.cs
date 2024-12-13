using System.Reflection;
using System.Runtime.InteropServices;

namespace Java.Interop.Samples.NativeAotFromAndroid;

class Runtime
{
    static MethodInfo? activate;

    [UnmanagedCallersOnly (EntryPoint="Java_mono_android_Runtime_register")]
    public static void Register (IntPtr jnienv, IntPtr managedType, IntPtr nativeClass, IntPtr methods)
    {
        AndroidLog.Print (AndroidLogLevel.Info, "Runtime", $"Register() called");

        //TODO: might need to call something
    }

    [UnmanagedCallersOnly (EntryPoint="Java_mono_android_TypeManager_n_1activate")]
    static void Activate (IntPtr jnienv, IntPtr jclass, IntPtr typename_ptr, IntPtr signature_ptr, IntPtr jobject, IntPtr parameters_ptr)
    {
        AndroidLog.Print (AndroidLogLevel.Info, "Runtime", $"Activate() called");

        try {
            activate ??= typeof(TypeManager).GetMethod ("n_Activate", BindingFlags.NonPublic | BindingFlags.Static);
            ArgumentNullException.ThrowIfNull (activate);
            activate.Invoke (null, [ jnienv, jclass, typename_ptr, signature_ptr, jobject, parameters_ptr ]);
        } catch (Exception exc) {
            AndroidLog.Print (AndroidLogLevel.Error, "Runtime", $"Activate() failed: {exc}");
        }
    }
}