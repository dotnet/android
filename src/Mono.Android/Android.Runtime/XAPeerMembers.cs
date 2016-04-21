using System;
using System.Collections.Generic;

using Java.Interop;

namespace Android.Runtime {

	public class XAPeerMembers : JniPeerMembers {

		static  Dictionary<string,  JniPeerMembers>         LegacyPeerMembers = new Dictionary<string, JniPeerMembers> ();

		public XAPeerMembers (string jniPeerTypeName, Type managedPeerType)
			: base (jniPeerTypeName, managedPeerType)
		{
		}

		protected override bool UsesVirtualDispatch (IJavaPeerable value, Type declaringType)
		{
			var peerType  = GetThresholdType (value);
			if (peerType != null) {
				return peerType == value.GetType ();
			}

			return base.UsesVirtualDispatch (value, declaringType);
		}

		protected override JniPeerMembers GetPeerMembers (IJavaPeerable value)
		{
			var peerType = GetThresholdType (value);
			if (peerType == null || value.JniPeerMembers.ManagedPeerType == peerType) {
				return base.GetPeerMembers (value);
			};

			var jniClass  = Java.Interop.TypeManager.GetClassName (GetThresholdClass (value));
			lock (LegacyPeerMembers) {
				JniPeerMembers members;
				if (!LegacyPeerMembers.TryGetValue (jniClass, out members)) {
					members = new XAPeerMembers (jniClass, peerType);
					LegacyPeerMembers.Add (jniClass, members);
				}
				return members;
			}
		}

		static Type GetThresholdType (IJavaPeerable value)
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

		static IntPtr GetThresholdClass (IJavaPeerable value)
		{
			var o = value as Java.Lang.Object;
			if (o != null) {
				return o.GetThresholdClass ();
			}
			var t = value as Java.Lang.Throwable;
			if (t != null) {
				return t.GetThresholdClass ();
			}
			return IntPtr.Zero;
		}
	}
}