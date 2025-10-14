using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Android.Runtime;

namespace Java.Interop
{
	class TypeMapAttributeValueManager : JniRuntime.JniValueManager
	{
		Dictionary<IntPtr, IdentityHashTargets> instances = new Dictionary<IntPtr, IdentityHashTargets> ();

		public override void WaitForGCBridgeProcessing ()
		{
			if (!AndroidRuntimeInternal.BridgeProcessing)
				return;
			RuntimeNativeMethods._monodroid_gc_wait_for_bridge_processing ();
		}

		public override IJavaPeerable? CreatePeer (
				ref JniObjectReference reference,
				JniObjectReferenceOptions options,
				[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
				Type? targetType)
		{
			if (!reference.IsValid)
				return null;

			var peer = CreateInstance (reference.Handle, JniHandleOwnership.DoNotTransfer, targetType) as IJavaPeerable;
			JniObjectReference.Dispose (ref reference, options);
			return peer;
		}

		internal static IJavaPeerable? CreateInstance (IntPtr handle, JniHandleOwnership transfer, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]Type? targetType)
		{
			Type? type = null;
			IntPtr class_ptr = JNIEnv.GetObjectClass (handle);
			string? class_name = TypeManager.GetClassName (class_ptr);
			System.Diagnostics.Debug.Assert (class_name == JNIEnv.GetClassNameFromInstance (handle));
			lock (TypeManagerMapDictionaries.AccessLock) {
				while (class_ptr != IntPtr.Zero) {
					if (class_name != null) {
						var class_signature = JniTypeSignature.Parse (class_name);
						type = JNIEnvInit.androidRuntime!.TypeManager.GetType (class_signature);
						if (type != null) {
							break;
						}
					}

					IntPtr super_class_ptr = JNIEnv.GetSuperclass (class_ptr);
					JNIEnv.DeleteLocalRef (class_ptr);
					class_name = null;
					class_ptr = super_class_ptr;
					if (class_ptr != IntPtr.Zero) {
						class_name = TypeManager.GetClassName (class_ptr);
					}
				}
			}

			if (class_ptr != IntPtr.Zero) {
				JNIEnv.DeleteLocalRef (class_ptr);
				class_ptr = IntPtr.Zero;
			}

			if (targetType != null &&
					(type == null ||
					 !targetType.IsAssignableFrom (type))) {
				type = targetType;
			}

			if (type == null) {
				class_name = JNIEnv.GetClassNameFromInstance (handle);
				JNIEnv.DeleteRef (handle, transfer);
				throw new NotSupportedException (
						FormattableString.Invariant ($"Internal error finding wrapper class for '{class_name}'. (Where is the Java.Lang.Object wrapper?!)"),
						TypeManager.CreateJavaLocationException ());
			}

			if (type.IsInterface || type.IsAbstract) {
				var invokerType = JavaObjectExtensions.GetInvokerType (type);
				if (invokerType == null)
					throw new NotSupportedException ("Unable to find Invoker for type '" + type.FullName + "'. Was it linked away?",
							TypeManager.CreateJavaLocationException ());
				type = invokerType;
			}

			var typeSig  = JNIEnvInit.androidRuntime?.TypeManager.GetTypeSignature (type) ?? default;
			if (!typeSig.IsValid || typeSig.SimpleReference == null) {
				throw new ArgumentException ($"Could not determine Java type corresponding to `{type.AssemblyQualifiedName}`.", nameof (targetType));
			}

			JniObjectReference typeClass = default;
			JniObjectReference handleClass = default;
			try {
				try {
					typeClass = JniEnvironment.Types.FindClass (typeSig.SimpleReference);
				} catch (Exception e) {
					throw new ArgumentException ($"Could not find Java class `{typeSig.SimpleReference}`.",
							nameof (targetType),
							e);
				}

				handleClass = JniEnvironment.Types.GetObjectClass (new JniObjectReference (handle));
				if (!JniEnvironment.Types.IsAssignableFrom (handleClass, typeClass)) {
					return null;
				}
			} finally {
				JniObjectReference.Dispose (ref handleClass);
				JniObjectReference.Dispose (ref typeClass);
			}

			IJavaPeerable? result = null;

			try {
				result = (IJavaPeerable) TypeManager.CreateProxy (type, handle, transfer);
				if (Java.Interop.Runtime.IsGCUserPeer (result.PeerReference.Handle)) {
					result.SetJniManagedPeerState (JniManagedPeerStates.Replaceable | JniManagedPeerStates.Activatable);
				}
			} catch (MissingMethodException e) {
				var key_handle  = JNIEnv.IdentityHash (handle);
				JNIEnv.DeleteRef (handle, transfer);
				throw new NotSupportedException (FormattableString.Invariant (
					$"Unable to activate instance of type {type} from native handle 0x{handle:x} (key_handle 0x{key_handle:x})."), e);
			}
			return result;
 		}

		public override void AddPeer (IJavaPeerable value)
		{
			if (value == null)
				throw new ArgumentNullException (nameof (value));
			if (!value.PeerReference.IsValid)
				throw new ArgumentException ("Must have a valid JNI object reference!", nameof (value));

			var reference = value.PeerReference;
			var hash = JNIEnv.IdentityHash (reference.Handle);

			AddPeer (value, reference, hash);
		}

		internal void AddPeer (IJavaPeerable value, JniObjectReference reference, IntPtr hash)
		{
			lock (instances) {
				if (!instances.TryGetValue (hash, out var targets)) {
					targets = new IdentityHashTargets (value);
					instances.Add (hash, targets);
					return;
				}
				bool found = false;
				for (int i = 0; i < targets.Count; ++i) {
					IJavaPeerable? target;
					var wref = targets [i];
					if (ShouldReplaceMapping (wref!, reference, value, out target)) {
						found = true;
						targets [i] = IdentityHashTargets.CreateWeakReference (value);
						break;
					}
					if (JniEnvironment.Types.IsSameObject (value.PeerReference, target!.PeerReference)) {
						found = true;
						if (Logger.LogGlobalRef) {
							Logger.Log (LogLevel.Info, "monodroid-gref", FormattableString.Invariant (
								$"warning: not replacing previous registered handle {target.PeerReference} with handle {reference} for key_handle 0x{hash:x}"));
						}
					}
				}
				if (!found) {
					targets.Add (value);
				}
			}
		}

		internal void AddPeer (IJavaPeerable value, IntPtr handle, JniHandleOwnership transfer, out IntPtr handleField)
		{
			if (handle == IntPtr.Zero) {
				handleField = handle;
				return;
			}

			var transferType = transfer & (JniHandleOwnership.DoNotTransfer | JniHandleOwnership.TransferLocalRef | JniHandleOwnership.TransferGlobalRef);
			switch (transferType) {
				case JniHandleOwnership.DoNotTransfer:
					handleField = JNIEnv.NewGlobalRef (handle);
					break;
				case JniHandleOwnership.TransferLocalRef:
					handleField = JNIEnv.NewGlobalRef (handle);
					JNIEnv.DeleteLocalRef (handle);
					break;
				case JniHandleOwnership.TransferGlobalRef:
					handleField = handle;
					break;
				default:
					throw new ArgumentOutOfRangeException ("transfer", transfer,
							"Invalid `transfer` value: " + transfer + " on type " + value.GetType ());
			}
			if (handleField == IntPtr.Zero)
				throw new InvalidOperationException ("Unable to allocate Global Reference for object '" + value.ToString () + "'!");

			IntPtr hash = JNIEnv.IdentityHash (handleField);
			value.SetJniIdentityHashCode ((int) hash);
			if ((transfer & JniHandleOwnership.DoNotRegister) == 0) {
				AddPeer (value, new JniObjectReference (handleField, JniObjectReferenceType.Global), hash);
			}

			if (Logger.LogGlobalRef) {
				RuntimeNativeMethods._monodroid_gref_log (
					FormattableString.Invariant (
						$"handle 0x{handleField:x}; key_handle 0x{hash:x}: Java Type: `{JNIEnv.GetClassNameFromInstance (handleField)}`; MCW type: `{value.GetType ().FullName}`\n"));
			}
		}

		bool ShouldReplaceMapping (WeakReference<IJavaPeerable> current, JniObjectReference reference, IJavaPeerable value, out IJavaPeerable? target)
		{
			target = null;

			if (current == null)
				return true;

			// Target has been GC'd; see also FIXME, above, in finalizer
			if (!current.TryGetTarget (out target) || target == null)
				return true;

			// It's possible that the instance was GC'd, but the finalizer
			// hasn't executed yet, so the `instances` entry is stale.
			if (!target.PeerReference.IsValid)
				return true;

			if (!JniEnvironment.Types.IsSameObject (target.PeerReference, reference))
				return false;

			// JNIEnv.NewObject/JNIEnv.CreateInstance() compatibility.
			// When two MCW's are created for one Java instance [0],
			// we want the 2nd MCW to replace the 1st, as the 2nd is
			// the one the dev created; the 1st is an implicit intermediary.
			//
			// Meanwhile, a new "replaceable" instance should *not* replace an
			// existing "replaceable" instance; see dotnet/android#9862.
			//
			// [0]: If Java ctor invokes overridden virtual method, we'll
			// transition into managed code w/o a registered instance, and
			// thus will create an "intermediary" via
			// (IntPtr, JniHandleOwnership) .ctor.
			if (target.JniManagedPeerState.HasFlag (JniManagedPeerStates.Replaceable) &&
					!value.JniManagedPeerState.HasFlag (JniManagedPeerStates.Replaceable)) {
				return true;
			}

			return false;
		}

		public override void RemovePeer (IJavaPeerable value)
		{
			if (value == null)
				throw new ArgumentNullException (nameof (value));

			var reference = value.PeerReference;
			if (!reference.IsValid) {
				// Likely an idempotent DIspose(); ignore.
				return;
			}
			var hash = JNIEnv.IdentityHash (reference.Handle);

			RemovePeer (value, hash);
		}

		internal void RemovePeer (IJavaPeerable value, IntPtr hash)
		{
			lock (instances) {
				if (!instances.TryGetValue (hash, out var targets)) {
					return;
				}
				for (int i = targets.Count - 1; i >= 0; i--) {
					var wref = targets [i];
					if (!wref!.TryGetTarget (out var target)) {
						// wref is invalidated; remove it.
						targets.RemoveAt (i);
						continue;
					}
					if (!object.ReferenceEquals (target, value)) {
						continue;
					}
					targets.RemoveAt (i);
				}
				if (targets.Count == 0) {
					instances.Remove (hash);
				}
			}
		}

		public override IJavaPeerable? PeekPeer (JniObjectReference reference)
		{
			if (!reference.IsValid)
				return null;

			var hash = JNIEnv.IdentityHash (reference.Handle);
			lock (instances) {
				if (instances.TryGetValue (hash, out var targets)) {
					for (int i = targets.Count - 1; i >= 0; i--) {
						var wref = targets [i];
						if (!wref!.TryGetTarget (out var result) || !result.PeerReference.IsValid) {
							targets.RemoveAt (i);
							continue;
						}
						if (!JniEnvironment.Types.IsSameObject (reference, result.PeerReference))
							continue;
						return result;
					}
				}
			}
			return null;
		}

		public override void ActivatePeer (IJavaPeerable? self, JniObjectReference reference, ConstructorInfo cinfo, object? []? argumentValues)
		{
			TypeManager.Activate (reference.Handle, cinfo, argumentValues);
		}

		protected override bool TryUnboxPeerObject (IJavaPeerable value, [NotNullWhen (true)] out object? result)
		{
			var proxy = value as JavaProxyThrowable;
			if (proxy != null) {
				result = proxy.InnerException;
				return true;
			}
			return base.TryUnboxPeerObject (value, out result);
		}

		public override void CollectPeers ()
		{
			GC.Collect ();
		}

		public override void FinalizePeer (IJavaPeerable value)
		{
			if (value == null)
				throw new ArgumentNullException (nameof (value));

			if (Logger.LogGlobalRef) {
				RuntimeNativeMethods._monodroid_gref_log (
						string.Format (CultureInfo.InvariantCulture,
							"Finalizing Instance.Type={0} PeerReference={1} IdentityHashCode=0x{2:x} Instance=0x{3:x}",
							value.GetType ().ToString (),
							value.PeerReference.ToString (),
							value.JniIdentityHashCode,
							RuntimeHelpers.GetHashCode (value)));
			}

			// FIXME: need hash cleanup mechanism.
			// Finalization occurs after a test of java persistence.  If the
			// handle still contains a java reference, we can't finalize the
			// object and should "resurrect" it.
			if (value.PeerReference.IsValid) {
				GC.ReRegisterForFinalize (value);
			} else {
				RemovePeer (value, (IntPtr) value.JniIdentityHashCode);
				value.SetPeerReference (new JniObjectReference ());
				value.Finalized ();
			}
		}

		public override List<JniSurfacedPeerInfo> GetSurfacedPeers ()
		{
			lock (instances) {
				var surfacedPeers = new List<JniSurfacedPeerInfo> (instances.Count);
				foreach (var e in instances) {
					for (int i = 0; i < e.Value.Count; i++) {
						var value = e.Value [i];
						surfacedPeers.Add (new JniSurfacedPeerInfo (e.Key.ToInt32 (), value!));
					}
				}
				return surfacedPeers;
			}
		}
	}
}
