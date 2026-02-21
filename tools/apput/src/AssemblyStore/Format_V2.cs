using System;
using System.Collections.Generic;
using System.IO;

namespace ApplicationUtility;

class Format_V2 : FormatBase
{
	protected override string LogTag => "AssemblyStore/Format_V2";

	public Format_V2 (Stream storeStream, string? description)
		: base (storeStream, description)
	{}

	protected override bool ReadAssemblies (BinaryReader reader, out IList<ApplicationAssembly>? assemblies)
	{
		throw new NotImplementedException ();
	}

	protected override IAspectState ValidateInner ()
	{
		throw new NotImplementedException ();
	}
}
