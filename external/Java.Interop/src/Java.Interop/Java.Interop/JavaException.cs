using System;

namespace Java.Interop
{
	[JniTypeSignature (JniTypeName)]
	unsafe public class JavaException : Exception, IJavaPeerable, IJavaPeerableEx
	{
		internal    const   string          JniTypeName = "java/lang/Throwable";
		readonly    static  JniPeerMembers  _members    = new JniPeerMembers (JniTypeName, typeof (JavaException));

		int     identity;
		bool    registered;
		string  javaStackTrace;

#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
		JniObjectReference  reference;
#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
#if FEATURE_JNIOBJECTREFERENCE_INTPTRS
		IntPtr                  handle;
		JniObjectReferenceType  handle_type;
#endif  // FEATURE_JNIOBJECTREFERENCE_INTPTRS

		protected   static  readonly    JniObjectReference*     InvalidJniObjectReference = null;

		public unsafe JavaException ()
		{
			var peer = JniPeerMembers.InstanceMethods.StartCreateInstance ("()V", GetType (), null);
			using (SetPeerReference (
					ref peer,
					JniObjectReferenceOptions.DisposeSourceReference)) {
				JniPeerMembers.InstanceMethods.FinishCreateInstance ("()V", this, null);
			}
			javaStackTrace    = _GetJavaStack (PeerReference);
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
				using (SetPeerReference (
						ref peer,
						JniObjectReferenceOptions.DisposeSourceReference)) {
					JniPeerMembers.InstanceMethods.FinishCreateInstance (signature, this, args);
				}
			} finally {
				JniEnvironment.References.Dispose (ref native_message, JniObjectReferenceOptions.DisposeSourceReference);
			}
			javaStackTrace    = _GetJavaStack (PeerReference);
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
				using (SetPeerReference (
						ref peer,
						JniObjectReferenceOptions.DisposeSourceReference)) {
					JniPeerMembers.InstanceMethods.FinishCreateInstance (signature, this, args);
				}
			} finally {
				JniEnvironment.References.Dispose (ref native_message, JniObjectReferenceOptions.DisposeSourceReference);
			}
			javaStackTrace    = _GetJavaStack (PeerReference);
		}

		public JavaException (ref JniObjectReference reference, JniObjectReferenceOptions transfer)
			: base (_GetMessage (ref reference, transfer), _GetCause (ref reference, transfer))
		{
			if (transfer == JniObjectReferenceOptions.Invalid)
				return;

			if (!reference.IsValid)
				return;
			using (SetPeerReference (ref reference, transfer)) {
			}
			javaStackTrace    = _GetJavaStack (PeerReference);
		}

		~JavaException ()
		{
			JniEnvironment.Runtime.TryCollectObject (this);
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

		public string JavaStackTrace {
			get {return javaStackTrace;}
		}

		public override string StackTrace {
			get {
				return base.StackTrace + Environment.NewLine +
					"  --- End of managed " + GetType ().FullName + " stack trace ---" + Environment.NewLine +
					javaStackTrace;
			}
		}

		protected SetSafeHandleCompletion SetPeerReference (ref JniObjectReference handle, JniObjectReferenceOptions transfer)
		{
			return JniEnvironment.Runtime.SetObjectPeerReference (
					this,
					ref handle,
					transfer,
					a => new SetSafeHandleCompletion (a));
		}

		public void UnregisterFromRuntime ()
		{
			if (!PeerReference.IsValid)
				throw new ObjectDisposedException (GetType ().FullName);
			JniEnvironment.Runtime.UnRegisterObject (this);
		}

		public void Dispose ()
		{
			if (PeerReference.Handle == IntPtr.Zero)
				return;
			JniEnvironment.Runtime.DisposeObject (this);
			var inner = InnerException as JavaException;
			if (inner != null) {
				inner.Dispose ();
			}
		}

		public void DisposeUnlessRegistered ()
		{
			if (registered)
				return;
			Dispose ();
		}

		protected virtual void Dispose (bool disposing)
		{
		}

		public override bool Equals (object obj)
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
			return _members.InstanceMethods.InvokeVirtualInt32Method ("hashCode\u0000()I", this, null);
		}

		static string _GetMessage (ref JniObjectReference reference, JniObjectReferenceOptions transfer)
		{
			if (transfer == JniObjectReferenceOptions.Invalid)
				return null;

			var m = _members.InstanceMethods.GetMethodInfo ("getMessage\u0000()Ljava/lang/String;");
			var s = m.InvokeVirtualObjectMethod (reference);
			return JniEnvironment.Strings.ToString (ref s, JniObjectReferenceOptions.DisposeSourceReference);
		}

		static Exception _GetCause (ref JniObjectReference reference, JniObjectReferenceOptions transfer)
		{
			if (transfer == JniObjectReferenceOptions.Invalid)
				return null;

			var m = _members.InstanceMethods.GetMethodInfo ("getCause\u0000()Ljava/lang/Throwable;");
			var e = m.InvokeVirtualObjectMethod (reference);
			return JniEnvironment.Runtime.GetExceptionForThrowable (ref e, JniObjectReferenceOptions.DisposeSourceReference);
		}

		unsafe string _GetJavaStack (JniObjectReference handle)
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
						var pst = _members.InstanceMethods.GetMethodInfo ("printStackTrace\u0000(Ljava/io/PrintWriter;)V");
						var pst_args = stackalloc JniArgumentValue [1];
						pst_args [0] = new JniArgumentValue (pwriter);
						pst.InvokeVirtualVoidMethod (handle, pst_args);
						var s = JniEnvironment.Object.ToString (swriter);
						return JniEnvironment.Strings.ToString (ref s, JniObjectReferenceOptions.DisposeSourceReference);
					} finally {
						JniEnvironment.References.Dispose (ref pwriter, JniObjectReferenceOptions.DisposeSourceReference);
					}
				} finally {
					JniEnvironment.References.Dispose (ref swriter, JniObjectReferenceOptions.DisposeSourceReference);
				}
			}
		}

		int IJavaPeerableEx.IdentityHashCode {
			get {return identity;}
			set {identity = value;}
		}

		bool IJavaPeerableEx.Registered {
			get {return registered;}
			set {registered = value;}
		}

		void IJavaPeerableEx.Dispose (bool disposing)
		{
			Dispose (disposing);
		}

		void IJavaPeerableEx.SetPeerReference (JniObjectReference reference)
		{
#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
			this.reference  = reference;
#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
#if FEATURE_JNIOBJECTREFERENCE_INTPTRS
			this.handle         = reference.Handle;
			this.handle_type    = reference.Type;
#endif  // FEATURE_JNIOBJECTREFERENCE_INTPTRS
		}

		protected struct SetSafeHandleCompletion : IDisposable {

			readonly    Action  action;

			public SetSafeHandleCompletion (Action action)
			{
				this.action = action;
			}

			public void Dispose ()
			{
				if (action != null)
					action ();
			}
		}
	}
}

