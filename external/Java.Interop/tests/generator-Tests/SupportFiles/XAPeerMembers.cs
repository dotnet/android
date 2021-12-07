#if !JAVA_INTEROP1

using System;
using System.Collections.Generic;

using Java.Interop;

namespace Android.Runtime {

	public class XAPeerMembers : JniPeerMembers {

		static  Dictionary<string,  JniPeerMembers>         LegacyPeerMembers = new Dictionary<string, JniPeerMembers> (StringComparer.Ordinal);

		public XAPeerMembers (string jniPeerTypeName, Type managedPeerType)
			: base (jniPeerTypeName, managedPeerType)
		{
		}

		public XAPeerMembers (string jniPeerTypeName, Type managedPeerType, bool isInterface)
			: base (jniPeerTypeName, managedPeerType, isInterface)
		{
		}

		protected override bool UsesVirtualDispatch (IJavaPeerable value, Type declaringType)
		{
			return false;
		}

		static Type GetThresholdType (IJavaPeerable value)
		{
			return null;
		}

		static IntPtr GetThresholdClass (IJavaPeerable value)
		{
			return IntPtr.Zero;
		}
	}
}

#endif  // !JAVA_INTEROP1
