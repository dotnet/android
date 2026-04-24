using System.Collections.Generic;

namespace ApplicationUtility;

/// <summary>
/// Options controlling assembly extraction: target architectures, name patterns, PDB extraction, and decompression.
/// </summary>
public class AssemblyExtractorOptions
{
	public ICollection<NativeArchitecture>? Architectures { get; set; }
	public bool UseRegex { get; set; }
	public bool ExtractPDB { get; set; }
	public bool NoDecompress { get; set; }
	public string TargetDir { get; }
	public List<string>? AssemblyPatterns { get; }

	public AssemblyExtractorOptions (string targetDir, List<string>? assemblyPatterns)
	{
		TargetDir = targetDir;
		AssemblyPatterns = assemblyPatterns;
	}
}
