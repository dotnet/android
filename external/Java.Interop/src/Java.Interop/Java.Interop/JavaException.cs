using System;

namespace Java.Interop
{
	[JniTypeInfo (JavaException.JniTypeName)]
	public class JavaException : Exception, IJavaObject, IJavaObjectEx
	{
		internal    const   string          JniTypeName = "java/lang/Throwable";
		readonly    static  JniPeerMembers  _members    = new JniPeerMembers (JniTypeName, typeof (JavaException));

		int     identity;
		bool    registered;
		string  javaStackTrace;

		public JavaException ()
		{
			JavaVM.SetObjectSafeHandle (this, _NewObject (GetType (), JniPeerMembers), JniHandleOwnership.Transfer);
			javaStackTrace    = _GetJavaStack (SafeHandle);
		}

		public JavaException (string message)
			: base (message)
		{
			JavaVM.SetObjectSafeHandle (this, _NewObject (GetType (), JniPeerMembers), JniHandleOwnership.Transfer);
			javaStackTrace    = _GetJavaStack (SafeHandle);
		}

		public JavaException (string message, Exception innerException)
			: base (message, innerException)
		{
			JavaVM.SetObjectSafeHandle (this, _NewObject (GetType (), JniPeerMembers), JniHandleOwnership.Transfer);
			javaStackTrace    = _GetJavaStack (SafeHandle);
		}

		public JavaException (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
			: base (_GetMessage (handle), _GetCause (handle))
		{
			JavaVM.SetObjectSafeHandle (this, handle, transfer);
			javaStackTrace    = _GetJavaStack (SafeHandle);
		}

		~JavaException ()
		{
			JniEnvironment.Current.JavaVM.TryCollectObject (this);
		}

		public  JniReferenceSafeHandle  SafeHandle {get; private set;}

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

		public void RegisterWithVM ()
		{
			JniEnvironment.Current.JavaVM.RegisterObject (this);
		}

		public void Dispose ()
		{
			if (SafeHandle == null)
				return;
			JniEnvironment.Current.JavaVM.DisposeObject (this);
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
			var o = obj as IJavaObject;
			if (o != null)
				return JniEnvironment.Types.IsSameObject (SafeHandle, o.SafeHandle);
			return false;
		}

		public override int GetHashCode ()
		{
			return _members.CallInstanceInt32Method ("hashCode\u0000()I", this);
		}

		static string _GetMessage (JniReferenceSafeHandle handle)
		{
			var m = _members.GetInstanceMethodID ("getMessage\u0000()Ljava/lang/String;");
			var s = m.CallVirtualObjectMethod (handle);
			return JniEnvironment.Strings.ToString (s, JniHandleOwnership.Transfer);
		}

		static JavaException _GetCause (JniReferenceSafeHandle handle)
		{
			var m = _members.GetInstanceMethodID ("getCause\u0000()Ljava/lang/Throwable;");
			var e = m.CallVirtualObjectMethod (handle);
			return JniEnvironment.Current.JavaVM.GetObject<JavaException> (e, JniHandleOwnership.Transfer);
		}

		string _GetJavaStack (JniReferenceSafeHandle handle)
		{
			using (var StringWriter_class   = new JniType ("java/io/StringWriter"))
			using (var StringWriter_init    = StringWriter_class.GetConstructor ("()V"))
			using (var swriter              = StringWriter_class.NewObject (StringWriter_init))
			using (var PrintWriter_class    = new JniType ("java/io/PrintWriter"))
			using (var PrintWriter_init     = PrintWriter_class.GetConstructor ("(Ljava/io/Writer;)V"))
			using (var pwriter              = PrintWriter_class.NewObject (PrintWriter_init, new JValue (swriter))) {
				var pst = _members.GetInstanceMethodID ("printStackTrace\u0000(Ljava/io/PrintWriter;)V");
				pst.CallVirtualVoidMethod (handle, new JValue (pwriter));
				var s   = JniEnvironment.Current.Object_toString.CallVirtualObjectMethod (swriter);
				return JniEnvironment.Strings.ToString (s, JniHandleOwnership.Transfer);
			}
		}

		static JniLocalReference _NewObject (Type type, JniPeerMembers peerMembers)
		{
			var info    = JniEnvironment.Current.JavaVM.GetJniTypeInfoForType (type);
			if (info.JniTypeName == null)
				throw new NotSupportedException (
					string.Format ("Cannot create instance of type '{0}': no Java peer type found.",
						type.FullName));

			if (type == peerMembers.ManagedPeerType) {
				var c = peerMembers.GetConstructor ("()V");
				return peerMembers.JniPeerType.NewObject (c);
			}
			using (var t = new JniType (info.ToString ()))
			using (var c = t.GetConstructor ("()V")) {
				return t.NewObject (c);
			}
		}

		int IJavaObjectEx.IdentityHashCode {
			get {return identity;}
			set {identity = value;}
		}

		bool IJavaObjectEx.Registered {
			get {return registered;}
			set {registered = value;}
		}

		void IJavaObjectEx.Dispose (bool disposing)
		{
			Dispose (disposing);
		}

		void IJavaObjectEx.SetSafeHandle (JniReferenceSafeHandle handle)
		{
			SafeHandle = handle;
		}
	}
}

