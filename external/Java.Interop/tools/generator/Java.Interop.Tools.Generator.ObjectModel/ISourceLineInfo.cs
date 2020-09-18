using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoDroid.Generation
{
	public interface ISourceLineInfo
	{
		int LineNumber { get; set; }
		int LinePosition { get; set; }
		string SourceFile { get; set; }
	}
}
