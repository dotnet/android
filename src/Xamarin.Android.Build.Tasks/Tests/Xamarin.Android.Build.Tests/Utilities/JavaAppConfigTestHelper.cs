#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Xamarin.Android.Build.Tests;

static class JavaAppConfigTestHelper
{
	public static (byte [] Data, int [] Layout, string [] Strings, byte [] Flags) Read (string source)
	{
		source = Regex.Replace (source, @"//[^\r\n]*", "");
		byte [] data = ReadPrimitiveArray (source, "byte", "NativeConfig", "nativeConfig")
			.Select (value => unchecked ((byte)value)).ToArray ();
		int [] layout = ReadPrimitiveArray (source, "int", "NativeConfigLayout", "nativeConfigLayout");
		if (data.Length == 0 || layout.Length < 5 || layout [4] != 0) {
			throw new InvalidDataException ("Invalid Java bootstrap data");
		}
		var flagsMatch = Regex.Match (source, @"NativeLibraryFlags = new byte\[\] \{(?<values>.*?)\};", RegexOptions.Singleline);
		if (!flagsMatch.Success) {
			throw new InvalidDataException ("Missing Java native library flags");
		}
		byte [] flags = ReadIntegers (flagsMatch.Groups ["values"].Value).Select (value => checked ((byte)value)).ToArray ();
		if (flags.Length != layout [3]) {
			throw new InvalidDataException ("Invalid Java native library flag count");
		}
		var strings = new string [layout.Length - 4];
		var utf8 = new UTF8Encoding (encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
		for (int i = 0; i < strings.Length; i++) {
			int start = layout [i + 4];
			int end = i + 1 < strings.Length ? layout [i + 5] : data.Length;
			if (start < 0 || end > data.Length || start >= end || data [end - 1] != 0) {
				throw new InvalidDataException ("Invalid Java bootstrap string offsets");
			}
			strings [i] = utf8.GetString (data, start, end - start - 1);
		}
		return (data, layout, strings, flags);
	}

	static int [] ReadPrimitiveArray (string source, string javaType, string name, string methodPrefix)
	{
		var inline = Regex.Match (source, $@"{name} = new {javaType}\[\] \{{(?<values>.*?)\}};", RegexOptions.Singleline);
		if (inline.Success) {
			return ReadIntegers (inline.Groups ["values"].Value);
		}
		var chunks = Regex.Matches (source,
			$@"private static {javaType}\[\] {methodPrefix}Chunk\d+ \(\).*?return new {javaType}\[\] \{{(?<values>.*?)\}};",
			RegexOptions.Singleline);
		if (chunks.Count == 0) {
			throw new InvalidDataException ($"Missing Java bootstrap array {name}");
		}
		return chunks.Cast<Match> ().SelectMany (chunk => ReadIntegers (chunk.Groups ["values"].Value)).ToArray ();
	}

	static int [] ReadIntegers (string source) => source.Split ([','], StringSplitOptions.RemoveEmptyEntries)
		.Where (value => !string.IsNullOrWhiteSpace (value))
		.Select (value => int.Parse (value, CultureInfo.InvariantCulture)).ToArray ();
}
