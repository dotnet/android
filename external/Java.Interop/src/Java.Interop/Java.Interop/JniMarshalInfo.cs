using System;

namespace Java.Interop {

	public struct JniMarshalInfo {

		public Func<JniReferenceSafeHandle, JniHandleOwnership, Type, object>   MarshalFromJni;
		public Func<object, JniLocalReference>                                  MarshalToJni;
	}
}

