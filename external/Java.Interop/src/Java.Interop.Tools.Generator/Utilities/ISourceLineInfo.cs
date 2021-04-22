using System;

namespace Java.Interop.Tools.Generator
{
	public interface ISourceLineInfo
	{
		int LineNumber { get; set; }
		int LinePosition { get; set; }
		string SourceFile { get; set; }
	}
}
