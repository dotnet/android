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
			// Newly generated bindings use JniPeerMembers directly. XAPeerMembers is
			// retained for old binaries, but hand-written bindings may have used it
			// without declaring threshold overrides.
			if (value.JniPeerMembers is not XAPeerMembers)
				return base.UsesVirtualDispatch (value, declaringType);

			var peerType = GetThresholdType (value);
			// Old generated bindings return the managed type represented by their
			// JniPeerMembers. A mismatch means either there is no override, or this
			// is a new binding derived from an old one; both use metadata dispatch.
			if (peerType != value.JniPeerMembers.ManagedPeerType)
				return base.UsesVirtualDispatch (value, declaringType);

			return peerType == value.GetType ();
		}

		protected override JniPeerMembers GetPeerMembers (IJavaPeerable value)
		{
			// Retained because this protected override is part of the shipped API.
			return base.GetPeerMembers (value);
		}

		static Type? GetThresholdType (IJavaPeerable value)
		{
			if (value is Java.Lang.Object o) {
				return o.GetThresholdTypeForLegacyDispatch ();
			}
			if (value is Java.Lang.Throwable t) {
				return t.GetThresholdTypeForLegacyDispatch ();
			}
			return null;
		}
	}
}
