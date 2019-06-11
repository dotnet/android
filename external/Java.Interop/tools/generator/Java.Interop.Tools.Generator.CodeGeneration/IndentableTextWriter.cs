using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoDroid.Generation
{
	class IndentableTextWriter : StreamWriter
	{
		public int IndentCount { get; private set; }

		public IndentableTextWriter (Stream stream) : base (stream) { }
		public IndentableTextWriter (string path) : base (path) { }

		public void Indent (int count = 1) => IndentCount += count;

		public void Undent (int count = 1) => IndentCount = Math.Max (0, IndentCount - count);
	}
}
