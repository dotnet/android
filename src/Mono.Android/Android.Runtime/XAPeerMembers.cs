using System;
using System.Collections.Generic;

using Java.Interop;

namespace Android.Runtime {

	public class XAPeerMembers : JniPeerMembers {

		static readonly Dictionary<string, JniPeerMembers> LegacyPeerMembers = new Dictionary<string, JniPeerMembers> (StringComparer.Ordinal);

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
			if (value.JniPeerMembers is not XAPeerMembers) {
				return base.GetPeerMembers (value);
			}

			var peerType = GetThresholdType (value);
			if (peerType == null || value.JniPeerMembers.ManagedPeerType == peerType) {
				return base.GetPeerMembers (value);
			}

			var jniClass = Java.Interop.TypeManager.GetClassName (GetThresholdClass (value));
			lock (LegacyPeerMembers) {
				if (!LegacyPeerMembers.TryGetValue (jniClass, out var members)) {
					members = new XAPeerMembers (jniClass, peerType);
					LegacyPeerMembers.Add (jniClass, members);
				}
				return members;
			}
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

		static IntPtr GetThresholdClass (IJavaPeerable value)
		{
			if (value is Java.Lang.Object o) {
				return o.GetThresholdClass ();
			}
			if (value is Java.Lang.Throwable t) {
				return t.GetThresholdClass ();
			}
			return IntPtr.Zero;
		}
	}
}
