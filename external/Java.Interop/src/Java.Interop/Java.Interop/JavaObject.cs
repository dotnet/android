using System;

namespace Java.Interop
{
	[JniTypeInfo ("java/lang/Object")]
	public class JavaObject : IJavaObject, IJavaObjectEx
	{
		readonly static JniPeerMembers _members = new JniPeerMembers ("java/lang/Object", typeof (JavaObject));

		int     keyHandle;
		bool    registered;

		~JavaObject ()
		{
			JniEnvironment.Current.JavaVM.TryCollectObject (this);
		}

		public          JniReferenceSafeHandle      SafeHandle {get; private set;}

		// Note: JniPeerMembers is invoked virtually from the constructor;
		// it MUST be valid before the derived constructor executes!
		// The pattern MUST be followed.
		public  virtual JniPeerMembers              JniPeerMembers {
			get {return _members;}
		}

		public JavaObject (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
		{
			JavaVM.SetObjectSafeHandle (this, handle, transfer);
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

		public JavaObject ()
		{
			JavaVM.SetObjectSafeHandle (this, _NewObject (GetType (), JniPeerMembers), JniHandleOwnership.Transfer);
		}

		public void Register ()
		{
			JniEnvironment.Current.JavaVM.RegisterObject (this);
		}

		public void Dispose ()
		{
			JniEnvironment.Current.JavaVM.DisposeObject (this);
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
			return _members.CallInstanceInt32Method ("hashCode", "()I", "hashCode()I", this);
		}

		public override string ToString ()
		{
			var lref = _members.CallInstanceObjectMethod (
					"toString",
					"()Ljava/lang/String;",
					"toString()Ljava/lang/String;",
					this);
			return JniEnvironment.Strings.ToString (lref, JniHandleOwnership.Transfer);
		}

		int IJavaObjectEx.IdentityHashCode {
			get {return keyHandle;}
			set {keyHandle = value;}
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

