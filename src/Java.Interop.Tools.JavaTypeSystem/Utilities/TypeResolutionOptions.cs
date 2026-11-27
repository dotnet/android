using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Java.Interop.Tools.JavaTypeSystem
{
	public class TypeResolutionOptions
	{
		// We *should* do this, but ApiXmlAdjuster does not, so we have this flag for compatibility.
		// If a member on an interface cannot be resolved and needs to be removed, remove the whole interface.
		public bool RemoveInterfacesWithUnresolvableMembers { get; set; } = false;

		public static TypeResolutionOptions Default => new TypeResolutionOptions ();

		public static bool ResolveGenerics = true;
	}
}
