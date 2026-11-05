using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoDroid.Generation
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
	}
}
