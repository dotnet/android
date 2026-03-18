using System.Collections.Generic;

namespace ApplicationUtility;

/// <summary>
/// Stores state from <see cref="AssemblyPdb.ProbeAspect"/>, including Portable PDB metadata
/// header information and stream descriptors.
/// </summary>
class AssemblyPdbAspectState : BasicAspectState
{
	internal sealed class StreamInfo
	{
		public string Name { get; }
		public uint   Size { get; }

		public StreamInfo (string name, uint size)
		{
			Name = name;
			Size = size;
		}
	}

	public ICollection<StreamInfo>? Streams { get; }
	public string? FormatVersion            { get; }
	public uint FileMajorVersion            { get; }
	public uint FileMinorVersion            { get; }

	public AssemblyPdbAspectState (bool success)
		: base (success)
	{}

	public AssemblyPdbAspectState (string formatVersion, uint fileMajorVersion, uint fileMinorVersion, ICollection<StreamInfo> streams)
		: this (success: true)
	{
		FormatVersion = formatVersion;
		FileMajorVersion = fileMajorVersion;
		FileMinorVersion = fileMinorVersion;
		Streams = streams;
	}
}
