using System.Collections.Generic;

namespace ApplicationUtility;

public class AssemblyExtractorOptions
{
	public ICollection<NativeArchitecture>? Architectures { get; set; }
	public bool UseRegex { get; set; }
	public bool ExtractPDB { get; set; }
	public string TargetDir { get; }

	public AssemblyExtractorOptions (string targetDir)
	{
		TargetDir = targetDir;
	}
}
