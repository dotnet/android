using System;

namespace Java.Interop {

	interface IJavaObjectEx {
		int     IdentityHashCode    {get; set;}
		bool    Registered          {get; set;}

		void    Dispose (bool disposing);
		void    SetSafeHandle (JniReferenceSafeHandle handle);
	}
}
