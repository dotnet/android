using System;
using System.IO;

namespace ApplicationUtility;

class Format_V2 : FormatBase
{
	public Format_V2 (Stream storeStream, string? description)
		: base (storeStream, description)
	{}

	protected override IAspectState ValidateInner ()
	{
		throw new NotImplementedException ();
	}
}
