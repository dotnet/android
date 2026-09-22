using System.IO;
using System.Text;
using System.Runtime.InteropServices;

using Java.Interop;

namespace Microsoft.Android.Runtime;

struct DiagnosticSettings {

	public  bool    LogJniLocalReferences;
	private string? LrefPath;

	public  bool    LogJniGlobalReferences;
	private string? GrefPath;

	private TextWriter?     GrefLrefLog;


	public  TextWriter? GrefLog {
		get {
			if (!LogJniGlobalReferences) {
				return null;
			}
			return ((LrefPath != null && LrefPath == GrefPath)
					? GrefLrefLog ??= CreateWriter (LrefPath)
					: null)
				??
				((GrefPath != null)
					? CreateWriter (GrefPath)
					: null)
				??
				new LogcatTextWriter (AndroidLogLevel.Debug, "NativeAot:GREF");
		}
	}

	public  TextWriter? LrefLog {
		get {
			if (!LogJniLocalReferences) {
				return null;
			}
			return ((LrefPath != null && LrefPath == GrefPath)
					? GrefLrefLog ??= CreateWriter (LrefPath)
					: null)
				??
				((LrefPath != null)
					? CreateWriter (LrefPath)
					: null)
				??
				new LogcatTextWriter (AndroidLogLevel.Debug, "NativeAot:LREF");
		}
	}

	TextWriter? CreateWriter (string path)
	{
		try {
			return File.CreateText (path);
		}
		catch (Exception e) {
			AndroidLog.Print (AndroidLogLevel.Error, "NativeAot", $"Failed to open log file `{path}`: {e}");
			return null;
		}
	}

	public void AddDebugDotnetLog ()
	{
		Span<byte>  value   = stackalloc byte [RuntimeNativeMethods.PROP_VALUE_MAX];
		if (!RuntimeNativeMethods.TryGetSystemProperty ("debug.dotnet.log"u8, ref value)) {
			return;
		}
		AddParse (value);
	}

	void AddParse (ReadOnlySpan<byte> value)
	{
		while (TryGetNextValue (ref value, out var v)) {
			if (v.SequenceEqual ("lref"u8)) {
				LogJniLocalReferences = true;
			}
			else if (v.StartsWith ("lref="u8)) {
				LogJniLocalReferences = true;
				var path = v.Slice ("lref=".Length);
				LrefPath = Encoding.UTF8.GetString (path);
			}
			else if (v.SequenceEqual ("gref"u8)) {
				LogJniGlobalReferences = true;
			}
			else if (v.StartsWith ("gref="u8)) {
				LogJniGlobalReferences = true;
				var path = v.Slice ("gref=".Length);
				GrefPath = Encoding.UTF8.GetString (path);
			}
			else if (v.SequenceEqual ("all"u8)) {
				LogJniLocalReferences   = true;
				LogJniGlobalReferences  = true;
			}
		}

		bool TryGetNextValue (ref ReadOnlySpan<byte> value, out ReadOnlySpan<byte> next)
		{
			if (value.Length == 0) {
				next  = default;
				return false;
			}
			int c = value.IndexOf ((byte) ',');
			if (c >= 0) {
				next  = value.Slice (0, c);
				value = value.Slice (c + 1);
			}
			else {
				next  = value;
				value = default;
			}
			return true;
		}
	}
}

static partial class RuntimeNativeMethods {

	[LibraryImport ("c", EntryPoint="__system_property_get")]
	static private partial int system_property_get (ReadOnlySpan<byte> name, Span<byte> value);

	internal const int PROP_VALUE_MAX = 92;

	internal static bool TryGetSystemProperty (ReadOnlySpan<byte> name, ref Span<byte> value)
	{
		int len     = system_property_get (name, value);
		if (len <= 0) {
			return false;
		}

		value   = value.Slice (0, len);
		return true;
	}
}
