using System;

using Java.Interop;

namespace Java.InteropTests
{
	[JniTypeSignature (CallNonvirtualDerived2.JniTypeName)]
	public class CallNonvirtualDerived2 : CallNonvirtualDerived
	{
		internal new const string JniTypeName = "net/dot/jni/test/CallNonvirtualDerived2";

		public CallNonvirtualDerived2 ()
		{
		}
	}
}

