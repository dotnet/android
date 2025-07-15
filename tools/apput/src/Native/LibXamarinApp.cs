using System;
using System.IO;

namespace ApplicationUtility;

// TODO: make it an abstract class, we need to support different formats
class LibXamarinApp : SharedLibrary, IAspect
{
	LibXamarinApp (Stream stream, string description)
		: base (stream, description)
	{}

	public static new IAspect LoadAspect (Stream stream, IAspectState state, string? description)
	{
		if (String.IsNullOrEmpty (description)) {
			throw new ArgumentException ("Must be a shared library name", nameof (description));
		}

		if (!IsSupportedELFSharedLibrary (stream, description)) {
			throw new InvalidOperationException ("Stream is not a supported ELF shared library");
		}

		// TODO: this needs to be versioned
		return new LibXamarinApp (stream, description);
	}

	public static new IAspectState ProbeAspect (Stream stream, string? description)
	{
		IAspectState sharedLibState = SharedLibrary.ProbeAspect (stream, description);
		if (!sharedLibState.Success) {
			return sharedLibState;
		}

		// TODO: check for presence of a handful of fields and read at least `format_tag` to determine
		//       format version.
		throw new NotImplementedException ();
	}
}
