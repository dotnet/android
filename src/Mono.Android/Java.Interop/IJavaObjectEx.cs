using System;

namespace Java.Interop {

	interface IJavaObjectEx {
		IntPtr      KeyHandle         {get; set;}
		bool        IsProxy           {get; set;}
		bool        NeedsActivation   {get; set;}
		IntPtr      ToLocalJniHandle ();
	}
}

