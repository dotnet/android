using System;

namespace Java.Interop.Tools.Generator
{
	public class CsvParser
	{
		readonly string [] fields;

		public CsvParser (string line)
		{
			fields = line.Split (',');
		}

		public string GetField (int index)
		{
			if (index >= fields.Length)
				return string.Empty;

			return fields [index].Trim ();
		}

		public AndroidSdkVersion GetFieldAsAndroidSdkVersion (int index)
		{
			return AndroidSdkVersion.Parse (GetField (index));
		}

		public AndroidSdkVersion? GetFieldAsNullableAndroidSdkVersion (int index)
		{
			var value = GetField (index);

			if (AndroidSdkVersion.TryParse (value, out var val))
				return val;

			return default;
		}
	}
}
