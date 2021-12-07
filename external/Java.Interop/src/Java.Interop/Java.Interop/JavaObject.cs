#nullable enable

using System;

namespace Java.Interop
{
	[JniTypeSignature ("java/lang/Object")]
	unsafe public class JavaObject : IJavaPeerable
	{
		readonly static JniPeerMembers _members = new JniPeerMembers ("java/lang/Object", typeof (JavaObject));

		public int                  JniIdentityHashCode { get; private set; }
		public JniManagedPeerStates JniManagedPeerState { get; private set; }

#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
		JniObjectReference  reference;
#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
#if FEATURE_JNIOBJECTREFERENCE_INTPTRS
		IntPtr                  handle;
		JniObjectReferenceType  handle_type;
	#pragma warning disable 0169
		// Used by JavaInteropGCBridge
		IntPtr                  weak_handle;
		int                     refs_added;
	#pragma warning restore 0169
#endif  // FEATURE_JNIOBJECTREFERENCE_INTPTRS

		protected   static  readonly    JniObjectReference*     InvalidJniObjectReference  = null;

		~JavaObject ()
		{
			JniEnvironment.Runtime.ValueManager.FinalizePeer (this);
		}

		public          JniObjectReference          PeerReference {
			get {
#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
				return reference;
#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
#if FEATURE_JNIOBJECTREFERENCE_INTPTRS
				return new JniObjectReference (handle, handle_type);
#endif  // FEATURE_JNIOBJECTREFERENCE_INTPTRS
			}
		}

		// Note: JniPeerMembers is invoked virtually from the constructor;
		// it MUST be valid before the derived constructor executes!
		// The pattern MUST be followed.
		public  virtual JniPeerMembers              JniPeerMembers {
			get {return _members;}
		}

		public JavaObject (ref JniObjectReference reference, JniObjectReferenceOptions options)
		{
			Construct (ref reference, options);
		}

		public unsafe JavaObject ()
			: this (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
			if (PeerReference.IsValid)
				return;

			var peer = JniPeerMembers.InstanceMethods.StartCreateInstance ("()V", GetType (), null);
			Construct (ref peer, JniObjectReferenceOptions.CopyAndDispose);
			JniPeerMembers.InstanceMethods.FinishCreateInstance ("()V", this, null);
		}

		protected void Construct (ref JniObjectReference reference, JniObjectReferenceOptions options)
		{
			JniEnvironment.Runtime.ValueManager.ConstructPeer (this, ref reference, options);
		}

		protected void SetPeerReference (ref JniObjectReference reference, JniObjectReferenceOptions options)
		{
			if (options == JniObjectReferenceOptions.None) {
				((IJavaPeerable) this).SetPeerReference (new JniObjectReference ());
				return;
			}

#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
			this.reference      = reference;
#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
#if FEATURE_JNIOBJECTREFERENCE_INTPTRS
			this.handle         = reference.Handle;
			this.handle_type    = reference.Type;
#endif  // FEATURE_JNIOBJECTREFERENCE_INTPTRS

			JniObjectReference.Dispose (ref reference, options);
		}

		public void UnregisterFromRuntime ()
		{
			if (!PeerReference.IsValid)
				throw new ObjectDisposedException (GetType ().FullName);
			JniEnvironment.Runtime.ValueManager.RemovePeer (this);
		}

		public void Dispose ()
		{
			JniEnvironment.Runtime.ValueManager.DisposePeer (this);
		}

		public virtual void DisposeUnlessReferenced ()
		{
			JniEnvironment.Runtime.ValueManager.DisposePeerUnlessReferenced (this);
		}

		protected virtual void Dispose (bool disposing)
		{
		}

		public override bool Equals (object? obj)
		{
			JniPeerMembers.AssertSelf (this);

			if (object.ReferenceEquals (obj, this))
				return true;
			var o = obj as IJavaPeerable;
			if (o != null)
				return JniEnvironment.Types.IsSameObject (PeerReference, o.PeerReference);
			return false;
		}

		public override unsafe int GetHashCode ()
		{
			return _members.InstanceMethods.InvokeVirtualInt32Method ("hashCode.()I", this, null);
		}

		public override unsafe string? ToString ()
		{
			var lref = _members.InstanceMethods.InvokeVirtualObjectMethod (
					"toString.()Ljava/lang/String;",
					this,
					null);
			return JniEnvironment.Strings.ToString (ref lref, JniObjectReferenceOptions.CopyAndDispose);
		}

		void IJavaPeerable.Disposed ()
		{
			Dispose (disposing: true);
		}

		void IJavaPeerable.Finalized ()
		{
			Dispose (disposing: false);
		}

		void IJavaPeerable.SetJniIdentityHashCode (int value)
		{
			JniIdentityHashCode = value;
		}

		void IJavaPeerable.SetJniManagedPeerState (JniManagedPeerStates value)
		{
			JniManagedPeerState = value;
		}

		void IJavaPeerable.SetPeerReference (JniObjectReference reference)
		{
			SetPeerReference (ref reference, JniObjectReferenceOptions.Copy);
		}
	}
}

