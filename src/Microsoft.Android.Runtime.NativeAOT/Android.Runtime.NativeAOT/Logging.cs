// NOTE: logging methods below are need temporarily due to:
// 1) linux-bionic BCL doesn't redirect stdout/stderr to logcat
// 2) Android.Util.Log won't work until we initialize the Java.Interop.JreRuntime

using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Android.Runtime;

internal sealed class LogcatTextWriter : TextWriter {

	public static void Init ()
	{
		// This method is a no-op, but it's necessary to ensure the static
		// constructor is executed.
	}

	static LogcatTextWriter ()
	{
		Console.SetOut (new LogcatTextWriter (AndroidLogLevel.Info));
		Console.SetError (new LogcatTextWriter (AndroidLogLevel.Error));
	}

	AndroidLogLevel Level;
	string          Tag;

	internal LogcatTextWriter (AndroidLogLevel level, string tag = "NativeAotFromAndroid")
	{
		Level = level;
		Tag   = tag;
	}

	public override Encoding Encoding => Encoding.UTF8;
	public override string NewLine => "\n";

	public override void WriteLine (string? value)
	{
		if (value == null) {
			AndroidLog.Print (Level, Tag, "");
			return;
		}
		ReadOnlySpan<char> span = value;
		while (!span.IsEmpty) {
			if (span.IndexOf ('\n') is int n && n < 0) {
				break;
			}
			var line    = span.Slice (0, n);
			AndroidLog.Print (Level, Tag, line.ToString ());
			span        = span.Slice (n + 1);
		}
		AndroidLog.Print (Level, Tag, span.ToString ());
	}
}

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