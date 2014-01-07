using System;
using System.Threading;

using Java.Interop;

namespace Java.Interop {

	public struct JniNativeMethodRegistration {

		public  string      Name;
		public  string      Signature;
		public  Delegate    Marshaler;

		public JniNativeMethodRegistration (string name, string signature, Delegate marshaler)
		{
			Name        = name;
			Signature   = signature;
			Marshaler   = marshaler;
		}
	}
}
