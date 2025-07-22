// NOTE: logging methods below are need temporarily because
// Android.Util.Log won't work until we initialize the Java.Interop.JreRuntime

using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Android.Runtime;

static class AndroidLog {

	[DllImport ("log", EntryPoint = "__android_log_print", CallingConvention = CallingConvention.Cdecl)]
	private static extern void __android_log_print(AndroidLogLevel level, string? tag, string format, string args, IntPtr ptr);

	internal static void Print(AndroidLogLevel level, string? tag, string message) =>
	    __android_log_print(level, tag, "%s", message, IntPtr.Zero);

}

internal enum AndroidLogLevel
{
    Unknown = 0x00,
    Default = 0x01,
    Verbose = 0x02,
    Debug   = 0x03,
    Info    = 0x04,
    Warn    = 0x05,
    Error   = 0x06,
    Fatal   = 0x07,
    Silent  = 0x08
}