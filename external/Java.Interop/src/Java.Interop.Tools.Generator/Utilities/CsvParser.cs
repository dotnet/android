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

		public int GetFieldAsInt (int index)
		{
			return int.Parse (GetField (index));
		}

		public int? GetFieldAsNullableInt32 (int index)
		{
			var value = GetField (index);

			if (int.TryParse (value, out var val))
				return val;

			return default;
		}
	}
}
