#nullable enable

using System;

namespace Java.Interop
{
	[JniTypeSignature (JniTypeName)]
	unsafe public class JavaException : Exception, IJavaPeerable
	{
		internal    const   string          JniTypeName = "java/lang/Throwable";
		readonly    static  JniPeerMembers  _members    = new JniPeerMembers (JniTypeName, typeof (JavaException));

		public string?                  JavaStackTrace { get; private set; }
		public int                      JniIdentityHashCode { get; private set; }
		public JniManagedPeerStates     JniManagedPeerState { get; private set; }

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

		protected   static  readonly    JniObjectReference*     InvalidJniObjectReference = null;

		public unsafe JavaException ()
			: this (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
			if (PeerReference.IsValid)
				return;

			var peer = JniPeerMembers.InstanceMethods.StartCreateInstance ("()V", GetType (), null);
			Construct (ref peer, JniObjectReferenceOptions.CopyAndDispose);
			JniPeerMembers.InstanceMethods.FinishCreateInstance ("()V", this, null);
			JavaStackTrace    = GetJavaStack (PeerReference);
		}

		public unsafe JavaException (string message)
			: base (message)
		{
			const string signature  = "(Ljava/lang/String;)V";
			var native_message = JniEnvironment.Strings.NewString (message);
			try {
				var args = stackalloc JniArgumentValue [1];
				args [0] = new JniArgumentValue (native_message);
				var peer = JniPeerMembers.InstanceMethods.StartCreateInstance (signature, GetType (), args);
				Construct (ref peer, JniObjectReferenceOptions.CopyAndDispose);
				JniPeerMembers.InstanceMethods.FinishCreateInstance (signature, this, args);
			} finally {
				JniObjectReference.Dispose (ref native_message, JniObjectReferenceOptions.CopyAndDispose);
			}
			JavaStackTrace    = GetJavaStack (PeerReference);
		}

		public unsafe JavaException (string message, Exception innerException)
			: base (message, innerException)
		{
			const string signature  = "(Ljava/lang/String;)V";
			var native_message  = JniEnvironment.Strings.NewString (message);
			try {
				var args = stackalloc JniArgumentValue [1];
				args [0] = new JniArgumentValue (native_message);
				var peer = JniPeerMembers.InstanceMethods.StartCreateInstance (signature, GetType (), args);
				Construct (ref peer, JniObjectReferenceOptions.CopyAndDispose);
				JniPeerMembers.InstanceMethods.FinishCreateInstance (signature, this, args);
			} finally {
				JniObjectReference.Dispose (ref native_message, JniObjectReferenceOptions.CopyAndDispose);
			}
			JavaStackTrace    = GetJavaStack (PeerReference);
		}

		public JavaException (ref JniObjectReference reference, JniObjectReferenceOptions transfer)
			: base (GetMessage (ref reference, transfer), GetCause (ref reference, transfer))
		{
			Construct (ref reference, transfer);
			if (PeerReference.IsValid)
				JavaStackTrace    = GetJavaStack (PeerReference);
		}

		protected void Construct (ref JniObjectReference reference, JniObjectReferenceOptions options)
		{
			JniEnvironment.Runtime.ValueManager.ConstructPeer (this, ref reference, options);
		}

		~JavaException ()
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

		public override string StackTrace {
			get {
				return base.StackTrace + Environment.NewLine +
					"  --- End of managed " + GetType ().FullName + " stack trace ---" + Environment.NewLine +
					JavaStackTrace;
			}
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

		public void DisposeUnlessReferenced ()
		{
			JniEnvironment.Runtime.ValueManager.DisposePeerUnlessReferenced (this);
		}

		protected virtual void Dispose (bool disposing)
		{
			var inner = InnerException as JavaException;
			if (inner != null) {
				inner.Dispose ();
			}
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

		static string? GetMessage (ref JniObjectReference reference, JniObjectReferenceOptions transfer)
		{
			if (transfer == JniObjectReferenceOptions.None)
				return null;

			var m = _members.InstanceMethods.GetMethodInfo ("getMessage.()Ljava/lang/String;");
			var s = JniEnvironment.InstanceMethods.CallObjectMethod (reference, m);
			return JniEnvironment.Strings.ToString (ref s, JniObjectReferenceOptions.CopyAndDispose);
		}

		static Exception? GetCause (ref JniObjectReference reference, JniObjectReferenceOptions transfer)
		{
			if (transfer == JniObjectReferenceOptions.None)
				return null;

			var m = _members.InstanceMethods.GetMethodInfo ("getCause.()Ljava/lang/Throwable;");
			var e = JniEnvironment.InstanceMethods.CallObjectMethod (reference, m);
			return JniEnvironment.Runtime.GetExceptionForThrowable (ref e, JniObjectReferenceOptions.CopyAndDispose);
		}

		unsafe string? GetJavaStack (JniObjectReference handle)
		{
			using (var StringWriter_class   = new JniType ("java/io/StringWriter"))
			using (var PrintWriter_class    = new JniType ("java/io/PrintWriter")) {
				var StringWriter_init       = StringWriter_class.GetConstructor ("()V");
				var PrintWriter_init        = PrintWriter_class.GetConstructor ("(Ljava/io/Writer;)V");
				var swriter                 = StringWriter_class.NewObject (StringWriter_init, null);
				try {
					var pwriter_args = stackalloc JniArgumentValue [1];
					pwriter_args [0] = new JniArgumentValue (swriter);
					var pwriter = PrintWriter_class.NewObject (PrintWriter_init, pwriter_args);
					try {
						var pst = _members.InstanceMethods.GetMethodInfo ("printStackTrace.(Ljava/io/PrintWriter;)V");
						var pst_args = stackalloc JniArgumentValue [1];
						pst_args [0] = new JniArgumentValue (pwriter);
						JniEnvironment.InstanceMethods.CallVoidMethod (handle, pst, pst_args);
						var s = JniEnvironment.Object.ToString (swriter);
						return JniEnvironment.Strings.ToString (ref s, JniObjectReferenceOptions.CopyAndDispose);
					} finally {
						JniObjectReference.Dispose (ref pwriter, JniObjectReferenceOptions.CopyAndDispose);
					}
				} finally {
					JniObjectReference.Dispose (ref swriter, JniObjectReferenceOptions.CopyAndDispose);
				}
			}
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

