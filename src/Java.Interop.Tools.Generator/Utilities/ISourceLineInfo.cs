using System;

namespace Java.Interop.Tools.Generator
{
	public interface ISourceLineInfo
	{
		int LineNumber { get; set; }
		int LinePosition { get; set; }
		string SourceFile { get; set; }
	}

	public class SourceLineInfo : ISourceLineInfo
	{
		public int LineNumber { get; set; }
		public int LinePosition { get; set; }
		public string SourceFile { get; set; }

		public SourceLineInfo (string sourceFile) : this (sourceFile, 0, 0) { }

		public SourceLineInfo (string sourceFile, int lineNumber, int linePosition)
		{
			SourceFile = sourceFile;
			LineNumber = lineNumber;
			LinePosition = linePosition;
		}
	}
}
