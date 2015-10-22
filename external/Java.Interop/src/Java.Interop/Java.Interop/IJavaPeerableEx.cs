using System;

namespace Java.Interop {

	interface IJavaPeerableEx {
		int     IdentityHashCode    {get; set;}
		bool    Registered          {get; set;}

		void    Dispose (bool disposing);
		void    SetPeerReference (JniObjectReference handle);
	}
}
