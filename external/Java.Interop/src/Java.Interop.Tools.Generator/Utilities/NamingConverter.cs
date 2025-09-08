using System;
using System.Linq;
using System.Xml.Linq;

namespace Java.Interop.Tools.Generator
{
	public static class NamingConverter
	{
		/// <summary>
		/// Converts a 'merge.SourceFile' attribute to an API level. (ex. "..\..\bin\BuildDebug\api\api-28.xml.in")
		/// </summary>
		public static AndroidSdkVersion ParseApiLevel (string? value)
		{
			var result = ExtractApiLevel (value);
			if (!result.HasValue ())
				return default;

			return result switch {
				"R" => new AndroidSdkVersion (30),
				"S" => new AndroidSdkVersion (31),
				_ => AndroidSdkVersion.Parse (result)
			};
		}

		static string? ExtractApiLevel (string? value)
		{
			if (!value.HasValue ())
				return null;

			var hyphen  = value.IndexOf ('-');
			if (hyphen < 0 || (hyphen+1) >= value.Length)
				return null;

			int end     = hyphen + 1;
			if (char.IsAsciiDigit (value [end++])) {
				for ( ; end < value.Length; ++end) {
					var n = value [end + 1];
					if (!char.IsAsciiDigit (n) && n != '.')
						break;
				}
			} else {
				// codename; expect ALLCAPS
				for ( ; end < value.Length; ++end) {
					if (!char.IsAsciiLetterUpper (value [end]))
						break;
				}
			}

			return value.Substring (hyphen + 1, end - hyphen - 1);
		}

		// The 'merge.SourceFile' attribute may be on the element, or only on its parent. For example,
		// a new 'class' added will only put the attribute on the '<class>' element and not its children <method>s.
		public static AndroidSdkVersion ParseApiLevel (XElement element)
		{
			var loop = element;

			while (loop != null) {
				if (loop.Attribute ("merge.SourceFile") is XAttribute attr)
					return ParseApiLevel (attr.Value);

				loop = loop.Parent;
			}

			return default;
		}

		public static string ConvertNamespaceToCSharp (string v)
		{
			return string.Join (".", v.Split ('.').Select (s => Capitalize (s)));
		}

		public static string ConvertClassToCSharp (string javaType)
		{
			return javaType;
		}

		public static string ConvertFieldToCSharp (string javaName)
		{
			// EX: FOREGROUND_SERVICE_IMMEDIATE
			return string.Join ("", javaName.Split ('_').Select (s => SentenceCase (s)));
		}

		public static string Capitalize (this string value)
		{
			if (value.Length < 1)
				return value;

			return char.ToUpperInvariant (value[0]) + value.Substring (1);
		}

		public static string SentenceCase (this string value)
		{
			return Capitalize (value.ToLowerInvariant ());
		}
	}
}
