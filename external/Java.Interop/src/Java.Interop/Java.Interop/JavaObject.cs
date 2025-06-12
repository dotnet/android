#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Java.Interop
{
	[JniTypeSignature ("java/lang/Object", GenerateJavaPeer=false)]
	[Serializable]
	unsafe public partial class JavaObject : IJavaPeerable
	{
		internal const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

		readonly static JniPeerMembers _members = new JniPeerMembers ("java/lang/Object", typeof (JavaObject));

		[NonSerialized] int                     identityHashCode;
		[NonSerialized] JniManagedPeerStates    managedPeerState;

		public int                  JniIdentityHashCode => identityHashCode;

		public JniManagedPeerStates JniManagedPeerState => managedPeerState;

#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
		[NonSerialized] JniObjectReference  reference;
#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
#if FEATURE_JNIOBJECTREFERENCE_INTPTRS
		[NonSerialized] unsafe  JniObjectReferenceControlBlock* jniObjectReferenceControlBlock;
#endif  // FEATURE_JNIOBJECTREFERENCE_INTPTRS

		protected   static  readonly    JniObjectReference*     InvalidJniObjectReference  = null;

		~JavaObject ()
		{
			JniEnvironment.Runtime.ValueManager.FinalizePeer (this);
		}

		public  unsafe  JniObjectReference          PeerReference {
			get {
#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
				return reference;
#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
#if FEATURE_JNIOBJECTREFERENCE_INTPTRS
				var c = jniObjectReferenceControlBlock;
				if (c == null) {
					return default;
				}
				return new JniObjectReference (c->handle, (JniObjectReferenceType) c->handle_type);
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

		[global::Java.Interop.JniConstructorSignature ("()V")]
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
			var c   = jniObjectReferenceControlBlock;
			if (c == null) {
				c   = jniObjectReferenceControlBlock    =
					Java.Interop.JniObjectReferenceControlBlock.Alloc (reference);
			} else {
				c->handle       = reference.Handle;
				c->handle_type  = (int) reference.Type;
			}
#endif  // FEATURE_JNIOBJECTREFERENCE_INTPTRS

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
			managedPeerState    |= Disposed;
			Dispose (disposing: true);
		}

		void IJavaPeerable.Finalized ()
		{
			managedPeerState    |= Disposed;
			Dispose (disposing: false);
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

#if FEATURE_JNIOBJECTREFERENCE_INTPTRS
			if (!reference.IsValid && managedPeerState.HasFlag (Disposed)) {
				Java.Interop.JniObjectReferenceControlBlock.Free (ref jniObjectReferenceControlBlock);
			}
#endif  // FEATURE_JNIOBJECTREFERENCE_INTPTRS
		}

		IntPtr IJavaPeerable.JniObjectReferenceControlBlock =>
			(IntPtr) jniObjectReferenceControlBlock;
	}
}

