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
		public static int ParseApiLevel (string? value)
		{
			if (!value.HasValue ())
				return 0;

			var hyphen = value.IndexOf ('-');
			var period = value.IndexOf ('.', hyphen);

			var result = value.Substring (hyphen + 1, period - hyphen - 1);

			return result switch {
				"R" => 30,
				"S" => 31,
				_ => int.Parse (result)
			};
		}

		// The 'merge.SourceFile' attribute may be on the element, or only on its parent. For example,
		// a new 'class' added will only put the attribute on the '<class>' element and not its children <method>s.
		public static int ParseApiLevel (XElement element)
		{
			var loop = element;

			while (loop != null) {
				if (loop.Attribute ("merge.SourceFile") is XAttribute attr)
					return ParseApiLevel (attr.Value);

				loop = loop.Parent;
			}

			return 0;
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
