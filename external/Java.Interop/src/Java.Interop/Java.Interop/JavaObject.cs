#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Java.Interop
{
	[JniTypeSignature ("java/lang/Object", GenerateJavaPeer=false)]
	[Serializable]
	public partial class JavaObject : IJavaPeerable
	{
		internal const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

		readonly static JniPeerMembers _members = new JniPeerMembers ("java/lang/Object", typeof (JavaObject));

		[NonSerialized] int                     identityHashCode;
		[NonSerialized] JniManagedPeerStates    managedPeerState;

		public int                  JniIdentityHashCode => identityHashCode;

		public JniManagedPeerStates JniManagedPeerState => managedPeerState;

		// Note: Remove unsafe with the memory-safety opt-in; required only for legacy pointer syntax.
		[NonSerialized] unsafe  JniObjectReferenceControlBlock* jniObjectReferenceControlBlock;

		protected   static  readonly    JniObjectReference*     InvalidJniObjectReference  = null;

		~JavaObject ()
		{
			JniEnvironment.Runtime.ValueManager.FinalizePeer (this);
		}

		public          JniObjectReference          PeerReference {
			get {
				unsafe {
					// SAFETY: Read this peer's control block only while its reference-manager lifetime permits access.
					var c = jniObjectReferenceControlBlock;
					if (c == null) {
						return default;
					}
					return new JniObjectReference (c->handle, (JniObjectReferenceType) c->handle_type);
				}
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

		protected JavaObject (JniObjectReferenceOptions options)
		{
			JniObjectReference reference = default;
			Construct (ref reference, options);
		}

		[global::Java.Interop.JniConstructorSignature ("()V")]
		public JavaObject ()
			: this (JniObjectReferenceOptions.None)
		{
			if (PeerReference.IsValid)
				return;

			unsafe {
				// SAFETY: ()V takes no arguments, so the argument pointer is null.
				// The current-thread local peer is copied/disposed before synchronous initialization uses this peer.
				var peer = JniPeerMembers.InstanceMethods.StartCreateInstance ("()V", GetType (), null);
				Construct (ref peer, JniObjectReferenceOptions.CopyAndDispose);
				JniPeerMembers.InstanceMethods.FinishCreateInstance ("()V", this, null);
			}
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

			unsafe {
				// SAFETY: This peer owns the allocated block; reference-manager updates must not race its release.
				var c   = jniObjectReferenceControlBlock;
				if (c == null) {
					c   = jniObjectReferenceControlBlock    =
						Java.Interop.JniObjectReferenceControlBlock.Alloc (reference);
				} else {
					c->handle       = reference.Handle;
					c->handle_type  = (int) reference.Type;
				}
			}
			JniObjectReference.Dispose (ref reference, options);
		}

		public void UnregisterFromRuntime ()
		{
			if (!PeerReference.IsValid)
				throw JniEnvironment.CreateObjectDisposedException (this);
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

		public override int GetHashCode ()
		{
			unsafe {
				// SAFETY: hashCode.()I has no arguments, so the virtual call does not read the null buffer.
				return _members.InstanceMethods.InvokeVirtualInt32Method ("hashCode.()I", this, null);
			}
		}

		public override string? ToString ()
		{
			JniObjectReference lref;
			unsafe {
				// SAFETY: toString takes no arguments; its returned local reference is consumed below.
				lref = _members.InstanceMethods.InvokeVirtualObjectMethod (
						"toString.()Ljava/lang/String;",
						this,
						null);
			}
			return JniEnvironment.Strings.ToString (ref lref, JniObjectReferenceOptions.CopyAndDispose);
		}

		void IJavaPeerable.Disposed ()
		{
			managedPeerState    |= Disposed;
			Dispose (disposing: true);
		}

		void IJavaPeerable.Finalized ()
		{
			managedPeerState    |= Disposed;
			try {
				Dispose (disposing: false);
			} finally {
				unsafe {
					// SAFETY: Finalization has detached tracking; FreeIfInvalid preserves a callback-reattached reference.
					Java.Interop.JniObjectReferenceControlBlock.FreeIfInvalid (ref jniObjectReferenceControlBlock);
				}
			}
		}

		void IJavaPeerable.SetJniIdentityHashCode (int value)
		{
			identityHashCode    = value;
		}

		void IJavaPeerable.SetJniManagedPeerState (JniManagedPeerStates value)
		{
			managedPeerState    = value;
		}

		void IJavaPeerable.SetPeerReference (JniObjectReference reference)
		{
			SetPeerReference (ref reference, JniObjectReferenceOptions.Copy);

			if (!reference.IsValid && managedPeerState.HasFlag (Disposed)) {
				unsafe {
					// SAFETY: Disposed-reference clearing releases this peer's block once and clears its owner slot.
					Java.Interop.JniObjectReferenceControlBlock.Free (ref jniObjectReferenceControlBlock);
				}
			}
		}

		IntPtr IJavaPeerable.JniObjectReferenceControlBlock {
			get {
				unsafe {
					// SAFETY: Return only the address; this getter neither dereferences it nor extends the block's lifetime.
					return (IntPtr) jniObjectReferenceControlBlock;
				}
			}
		}
	}
}
