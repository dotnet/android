using System.IO;

namespace tmt
{
	interface ITypemap
	{
		string Description              { get; }
		string FormatVersion            { get; }
		string FullPath                 { get; }
		Map Map                         { get; }
		MapArchitecture MapArchitecture { get; }

		bool CanLoad (Stream stream, string filePath);
		bool Load (string outputDirectory, bool generateFiles);
	}
}
