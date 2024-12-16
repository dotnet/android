using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Android.Runtime;

namespace Java.Interop.Samples.NativeAotFromAndroid;

class NativeAotValueManager : JniRuntime.JniValueManager {

    Dictionary<IntPtr, IdentityHashTargets>         instances       = new Dictionary<IntPtr, IdentityHashTargets> ();

    public override void WaitForGCBridgeProcessing ()
    {
        AndroidRuntimeInternal.WaitForBridgeProcessing ();
    }

    // public override IJavaPeerable? CreatePeer (
    //         ref JniObjectReference reference,
    //         JniObjectReferenceOptions options,
    //         [DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
    //         Type? targetType)
    // {
    //     if (!reference.IsValid)
    //         return null;

    //     var peer        = Java.Interop.TypeManager.CreateInstance (reference.Handle, JniHandleOwnership.DoNotTransfer, targetType) as IJavaPeerable;
    //     JniObjectReference.Dispose (ref reference, options);
    //     return peer;
    // }

    public override void AddPeer (IJavaPeerable value)
    {
        if (value == null)
            throw new ArgumentNullException (nameof (value));
        if (!value.PeerReference.IsValid)
            throw new ArgumentException ("Must have a valid JNI object reference!", nameof (value));

        var reference       = value.PeerReference;
        // TODO: probably wrong
        //var hash            = JNIEnv.IdentityHash (reference.Handle);
        var hash = reference.Handle;

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
                if (ShouldReplaceMapping (wref!, reference, out target)) {
                    found = true;
                    targets [i] = IdentityHashTargets.CreateWeakReference (value);
                    break;
                }
                if (JniEnvironment.Types.IsSameObject (value.PeerReference, target!.PeerReference)) {
                    found = true;
                    // if (Logger.LogGlobalRef) {
                    //     Logger.Log (LogLevel.Info, "monodroid-gref", FormattableString.Invariant (
                    //         $"warning: not replacing previous registered handle {target.PeerReference} with handle {reference} for key_handle 0x{hash:x}"));
                    // }
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

        //TODO: probably wrong
        //IntPtr hash = JNIEnv.IdentityHash (handleField);
        IntPtr hash = handleField;
        value.SetJniIdentityHashCode ((int) hash);
        if ((transfer & JniHandleOwnership.DoNotRegister) == 0) {
            AddPeer (value, new JniObjectReference (handleField, JniObjectReferenceType.Global), hash);
        }

        // if (Logger.LogGlobalRef) {
        //     RuntimeNativeMethods._monodroid_gref_log (
        //         FormattableString.Invariant (
        //             $"handle 0x{handleField:x}; key_handle 0x{hash:x}: Java Type: `{JNIEnv.GetClassNameFromInstance (handleField)}`; MCW type: `{value.GetType ().FullName}`\n"));
        // }
    }

    bool ShouldReplaceMapping (WeakReference<IJavaPeerable> current, JniObjectReference reference, out IJavaPeerable? target)
    {
        target      = null;

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
        // [0]: If Java ctor invokes overridden virtual method, we'll
        // transition into managed code w/o a registered instance, and
        // thus will create an "intermediary" via
        // (IntPtr, JniHandleOwnership) .ctor.
        if ((target.JniManagedPeerState & JniManagedPeerStates.Replaceable) == JniManagedPeerStates.Replaceable)
            return true;

        return false;
    }

    public override void RemovePeer (IJavaPeerable value)
    {
        if (value == null)
            throw new ArgumentNullException (nameof (value));

        var reference       = value.PeerReference;
        if (!reference.IsValid) {
            // Likely an idempotent DIspose(); ignore.
            return;
        }
        //TODO: probably wrong
        //var hash            = JNIEnv.IdentityHash (reference.Handle);
        var hash = reference.Handle;

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

        //TODO: probably wrong
        //var hash    = JNIEnv.IdentityHash (reference.Handle);
        var hash = reference.Handle;
        lock (instances) {
            if (instances.TryGetValue (hash, out var targets)) {
                for (int i = targets.Count - 1; i >= 0; i--) {
                    var wref    = targets [i];
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

    FieldInfo? object_handle_field;
    FieldInfo? throwable_handle_field;

    public override void ActivatePeer (IJavaPeerable? self, JniObjectReference reference, ConstructorInfo cinfo, object? []? argumentValues)
    {
        AndroidLog.Print (AndroidLogLevel.Info, "NativeAotValueManager", $"# jonp: ActivatePeer() {cinfo.DeclaringType}");

        var jobject = reference.Handle;
        try {
            var newobj = RuntimeHelpers.GetUninitializedObject (cinfo.DeclaringType!);
            if (newobj is Java.Lang.Object o) {
                //o.handle = jobject;
                object_handle_field ??= typeof(Java.Lang.Object).GetField ("handle", BindingFlags.Instance | BindingFlags.NonPublic);
                object_handle_field.SetValue (o, jobject);
            } else if (newobj is Java.Lang.Throwable throwable) {
                //throwable.handle = jobject;
                throwable_handle_field ??= typeof(Java.Lang.Throwable).GetField ("handle", BindingFlags.Instance | BindingFlags.NonPublic);
                throwable_handle_field.SetValue (throwable, jobject);
            } else {
                throw new InvalidOperationException ($"Unsupported type: '{newobj}'");
            }
            cinfo.Invoke (newobj, argumentValues);
        } catch (Exception e) {
            var m = FormattableString.Invariant (
                $"Could not activate JNI Handle 0x{jobject:x} as managed type '{cinfo?.DeclaringType?.FullName}'.");
            // Logger.Log (LogLevel.Warn, "monodroid", m);
            // Logger.Log (LogLevel.Warn, "monodroid", CreateJavaLocationException ().ToString ());

            throw new NotSupportedException (m, e);
        }
    }

    // TODO:
    // protected override bool TryUnboxPeerObject (IJavaPeerable value, [NotNullWhen (true)]out object? result)
    // {
    //     var proxy = value as JavaProxyThrowable;
    //     if (proxy != null) {
    //         result  = proxy.InnerException;
    //         return true;
    //     }
    //     return base.TryUnboxPeerObject (value, out result);
    // }

    internal Exception? UnboxException (IJavaPeerable value)
    {
        object? r;
        if (TryUnboxPeerObject (value, out r) && r is Exception e) {
            return e;
        }
        return null;
    }

    public override void CollectPeers ()
    {
        GC.Collect ();
    }

    public override void FinalizePeer (IJavaPeerable value)
    {
        if (value == null)
            throw new ArgumentNullException (nameof (value));

        // if (Logger.LogGlobalRef) {
        //     RuntimeNativeMethods._monodroid_gref_log (
        //             string.Format (CultureInfo.InvariantCulture,
        //                 "Finalizing Instance.Type={0} PeerReference={1} IdentityHashCode=0x{2:x} Instance=0x{3:x}",
        //                 value.GetType ().ToString (),
        //                 value.PeerReference.ToString (),
        //                 value.JniIdentityHashCode,
        //                 RuntimeHelpers.GetHashCode (value)));
        // }

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

    class IdentityHashTargets {
		WeakReference<IJavaPeerable>?            first;
		List<WeakReference<IJavaPeerable>?>?     rest;

		public static WeakReference<IJavaPeerable> CreateWeakReference (IJavaPeerable value)
		{
			return new WeakReference<IJavaPeerable> (value, trackResurrection: true);
		}

		public IdentityHashTargets (IJavaPeerable value)
		{
			first   = CreateWeakReference (value);
		}

		public int Count => (first != null ? 1 : 0) + (rest != null ? rest.Count : 0);

		public WeakReference<IJavaPeerable>? this [int index] {
			get {
				if (index == 0)
					return first;
				index -= 1;
				if (rest == null || index >= rest.Count)
					return null;
				return rest [index];
			}
			set {
				if (index == 0) {
					first = value;
					return;
				}
				index -= 1;

				if (rest != null)
					rest [index] = value;
			}
		}

		public void Add (IJavaPeerable value)
		{
			if (first == null) {
				first   = CreateWeakReference (value);
				return;
			}
			if (rest == null)
				rest    = new List<WeakReference<IJavaPeerable>?> ();
			rest.Add (CreateWeakReference (value));
		}

		public void RemoveAt (int index)
		{
			if (index == 0) {
				first   = null;
				if (rest?.Count > 0) {
					first   = rest [0];
					rest.RemoveAt (0);
				}
				return;
			}
			index -= 1;
			rest?.RemoveAt (index);
		}
	}
}