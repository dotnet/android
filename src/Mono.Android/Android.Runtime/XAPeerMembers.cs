using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Java.Interop;

namespace Android.Runtime {

	public class XAPeerMembers : JniPeerMembers {

		static  Dictionary<string,  JniPeerMembers>         LegacyPeerMembers = new Dictionary<string, JniPeerMembers> (StringComparer.Ordinal);
		readonly bool                                        hasThresholdTypeOverride;

		public XAPeerMembers (string jniPeerTypeName, Type managedPeerType)
			: base (jniPeerTypeName, managedPeerType)
		{
			hasThresholdTypeOverride = HasThresholdTypeOverride (managedPeerType);
		}

		public XAPeerMembers (string jniPeerTypeName, Type managedPeerType, bool isInterface)
			: base (jniPeerTypeName, managedPeerType, isInterface)
		{
			hasThresholdTypeOverride = HasThresholdTypeOverride (managedPeerType);
		}

		protected override bool UsesVirtualDispatch (IJavaPeerable value, Type? declaringType)
		{
			if (!UsesLegacyVirtualDispatch (value))
				return base.UsesVirtualDispatch (value, declaringType);

			var peerType  = GetThresholdType (value);
			if (peerType != null) {
				return peerType == value.GetType ();
			}

			return base.UsesVirtualDispatch (value, declaringType);
		}

		protected override JniPeerMembers GetPeerMembers (IJavaPeerable value)
		{
			if (!UsesLegacyVirtualDispatch (value))
				return base.GetPeerMembers (value);

			var peerType = GetThresholdType (value);
			if (peerType == null || value.JniPeerMembers.ManagedPeerType == peerType) {
				return base.GetPeerMembers (value);
			};

			var jniClass  = Java.Interop.TypeManager.GetClassName (GetThresholdClass (value));
			lock (LegacyPeerMembers) {
				if (!LegacyPeerMembers.TryGetValue (jniClass, out var members)) {
					members = new XAPeerMembers (jniClass, peerType);
					LegacyPeerMembers.Add (jniClass, members);
				}
				return members;
			}
		}

		bool UsesLegacyVirtualDispatch (IJavaPeerable value)
		{
			var peerMembers = value.JniPeerMembers as XAPeerMembers;
			return hasThresholdTypeOverride && peerMembers?.hasThresholdTypeOverride == true;
		}

		[UnconditionalSuppressMessage ("Trimming", "IL2070", Justification = "ThresholdType overrides remain reachable through GetThresholdType's virtual call.")]
		static bool HasThresholdTypeOverride (Type managedPeerType)
		{
			return managedPeerType.GetMethod ("get_ThresholdType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) != null;
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
