using System;

namespace Java.Interop.Tools.Generator
{
	public static class NamingConverter
	{
		/// <summary>
		/// Converts a 'merge.SourceFile' attribute to an API level. (ex. "..\..\bin\BuildDebug\api\api-28.xml.in")
		/// </summary>
		public static int ParseApiLevel (string value)
		{
			if (!value.HasValue ())
				return 0;

			var hyphen = value.IndexOf ('-');
			var period = value.IndexOf ('.', hyphen);

			var result = value.Substring (hyphen + 1, period - hyphen - 1);

			return int.Parse (result == "R" ? "30" : result);
		}
	}
}
