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
			var peerType = GetLegacyThresholdType (value);
			if (peerType == null)
				return base.UsesVirtualDispatch (value, declaringType);

			return peerType == value.GetType ();
		}

		protected override JniPeerMembers GetPeerMembers (IJavaPeerable value)
		{
			// Keep this shipped override for API compatibility. New dispatch uses the
			// receiver's peer members, which is the base implementation.
			return base.GetPeerMembers (value);
		}

		static Type? GetLegacyThresholdType (IJavaPeerable value)
		{
			// Old generated bindings override ThresholdType to return the managed type
			// represented by their JniPeerMembers. New bindings inherit Object's or
			// Throwable's value instead. Comparing the two also identifies a new binding
			// derived from an old one: it inherits the old ThresholdType but replaces
			// JniPeerMembers, so it must use the new metadata-based dispatch.
			var peerType = GetThresholdType (value);
			return peerType == value.JniPeerMembers.ManagedPeerType ? peerType : null;
		}

		static Type? GetThresholdType (IJavaPeerable value)
		{
			var o = value as Java.Lang.Object;
			if (o != null) {
				return o.GetThresholdType ();
			}
			var t = value as Java.Lang.Throwable;
			if (t != null) {
				return t.GetThresholdType ();
			}
			return null;
		}

	}
}
