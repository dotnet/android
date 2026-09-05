using System;

using Java.Interop;

namespace Android.Runtime {

	public class XAPeerMembers : JniPeerMembers {

		public XAPeerMembers (string jniPeerTypeName, Type managedPeerType)
			: base (jniPeerTypeName, managedPeerType)
		{
		}

		public XAPeerMembers (string jniPeerTypeName, Type managedPeerType, bool isInterface)
			: base (jniPeerTypeName, managedPeerType, isInterface)
		{
		}

		protected override bool UsesVirtualDispatch (IJavaPeerable value, Type? declaringType)
		{
			if (value.JniPeerMembers is XAPeerMembers) {
				var peerType = GetThresholdType (value);
				if (peerType != null) {
					return peerType == value.GetType ();
				}
			}

			return base.UsesVirtualDispatch (value, declaringType);
		}

		protected override JniPeerMembers GetPeerMembers (IJavaPeerable value)
		{
			// Retained because this protected override is part of the shipped API.
			return base.GetPeerMembers (value);
		}

		static Type? GetThresholdType (IJavaPeerable value)
		{
			if (value is Java.Lang.Object o) {
				return o.GetThresholdType ();
			}
			if (value is Java.Lang.Throwable t) {
				return t.GetThresholdType ();
			}
			return null;
		}
	}
}
