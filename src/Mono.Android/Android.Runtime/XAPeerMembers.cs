using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Java.Interop;

namespace Android.Runtime {

	public class XAPeerMembers : JniPeerMembers {

		static  Dictionary<string,  JniPeerMembers>         LegacyPeerMembers = new Dictionary<string, JniPeerMembers> (StringComparer.Ordinal);

		// -1: not yet determined; 0: no override; 1: declares an override.
		// Computed lazily so that type initialization -- which runs for every bound
		// type an app touches, on the startup path -- doesn't pay for the reflection
		// lookup unless a dispatch decision is actually made for this type.
		volatile int                                        thresholdOverrideState = -1;

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

		// Bindings compiled before the generated threshold overrides were removed declare
		// `ThresholdType`, and must keep the dispatch semantics they were compiled against.
		// Both sides have to opt in: the type declaring the method *and* the receiver. A new
		// binding deriving from an old one inherits the old `ThresholdType`, and honoring it
		// would dispatch nonvirtually to the Java base class, skipping the derived override.
		bool UsesLegacyVirtualDispatch (IJavaPeerable value)
		{
			if (!HasThresholdOverride)
				return false;
			var peerMembers = value.JniPeerMembers as XAPeerMembers;
			return peerMembers?.HasThresholdOverride == true;
		}

		bool HasThresholdOverride {
			get {
				var state = thresholdOverrideState;
				if (state < 0) {
					state = DeclaresThresholdTypeOverride (ManagedPeerType) ? 1 : 0;
					thresholdOverrideState = state;
				}
				return state == 1;
			}
		}

		// Checking `ThresholdType` alone is sufficient: every generator code path that emitted
		// threshold overrides (bound classes, class invokers, interface invokers) emitted this
		// one, and `ThresholdClass` was never emitted without it.
		[UnconditionalSuppressMessage ("Trimming", "IL2070",
				Justification = "ThresholdType overrides stay reachable through the virtual call in GetThresholdType(), so the trimmer preserves them on any type it keeps. Were one ever trimmed away, the type would silently fall back to metadata-based dispatch rather than fail.")]
		static bool DeclaresThresholdTypeOverride (Type managedPeerType)
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
