using System;
using System.Collections.Generic;
using System.IO;

namespace ApplicationUtility;

/// <summary>
/// Validator and reader for version 2 of the assembly store format. Currently unimplemented.
/// </summary>
class Format_V2 : FormatBase
{
	protected override string LogTag => "AssemblyStore/Format_V2";

	public Format_V2 (string? description)
		: base (description)
	{}

	protected override bool ReadAssemblies (BinaryReader reader, out IList<ApplicationAssembly>? assemblies, out IList<AssemblyPdb>? pdbs, out IDictionary<string, string>? configs)
	{
		throw new NotImplementedException ();
	}

	protected override IAspectState ValidateInner (Stream storeStream)
	{
		throw new NotImplementedException ();
	}
}
