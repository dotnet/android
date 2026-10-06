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
	public static (byte [] Data, int [] Layout, string [] Strings) Read (string source)
	{
		var chunks = Regex.Matches (source, @"private static byte\[\] nativeConfigChunk\d+ \(\).*?return new byte\[\] \{(?<values>.*?)\};", RegexOptions.Singleline);
		var inline = Regex.Match (source, @"NativeConfig = new byte\[\] \{(?<values>.*?)\};", RegexOptions.Singleline);
		var values = inline.Success
			? ReadIntegers (inline.Groups ["values"].Value)
			: chunks.Cast<Match> ().SelectMany (chunk => ReadIntegers (chunk.Groups ["values"].Value));
		byte [] data = values
			.Select (value => unchecked ((byte)value)).ToArray ();
		var layoutMatch = Regex.Match (source, @"NativeConfigLayout = new int\[\] \{(?<values>.*?)\};", RegexOptions.Singleline);
		int [] layout = ReadIntegers (layoutMatch.Groups ["values"].Value);
		if (data.Length == 0 || layout.Length < 5 || layout [4] != 0) {
			throw new InvalidDataException ("Invalid Java bootstrap data");
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
		return (data, layout, strings);
	}

	static int [] ReadIntegers (string source) => source.Split ([','], StringSplitOptions.RemoveEmptyEntries)
		.Where (value => !string.IsNullOrWhiteSpace (value))
		.Select (value => int.Parse (value, CultureInfo.InvariantCulture)).ToArray ();
}
