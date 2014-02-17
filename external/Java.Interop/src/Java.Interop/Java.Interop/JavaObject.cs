using System;

namespace Java.Interop
{
	[JniTypeInfo ("java/lang/Object")]
	public class JavaObject : IJavaObject
	{
		readonly static JniPeerMembers _members = new JniPeerMembers ("java/lang/Object", typeof (JavaObject));

		int     keyHandle;
		bool    registered;

		~JavaObject ()
		{
			var  h          = SafeHandle;
			bool collected  = JniEnvironment.Current.JavaVM.TryGC (this, ref h);
			if (collected) {
				SafeHandle = null;
				if (registered)
					JniEnvironment.Current.JavaVM.UnRegisterObject (keyHandle, this);
				Dispose (false);
			} else {
				SafeHandle = h;
				GC.ReRegisterForFinalize (this);
			}
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
			SetSafeHandle (handle, transfer);
		}

		void SetSafeHandle (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
		{
			if (handle == null)
				throw new ArgumentNullException ("handle");
			if (handle.IsInvalid)
				throw new ArgumentException ("handle is invalid.", "handle");

			SafeHandle = handle.NewLocalRef ();
			JniEnvironment.Handles.Dispose (handle, transfer);

			keyHandle = JniSystem.IdentityHashCode (SafeHandle);
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
			SetSafeHandle (_NewObject (GetType (), JniPeerMembers), JniHandleOwnership.Transfer);
		}

		public void Register ()
		{
			if (SafeHandle == null || SafeHandle.IsInvalid)
				throw new ObjectDisposedException (GetType ().FullName);

			if (registered)
				return;

			if (SafeHandle.ReferenceType != JniReferenceType.Global) {
				var o = SafeHandle;
				SafeHandle = o.NewGlobalRef ();
				o.Dispose ();
			}
			JniEnvironment.Current.JavaVM.RegisterObject (keyHandle, this);
			registered = true;
		}

		public void Dispose ()
		{
			if (SafeHandle == null || SafeHandle.IsInvalid)
				return;

			Dispose (true);
			if (registered)
				JniEnvironment.Current.JavaVM.UnRegisterObject (keyHandle, this);
			SafeHandle.Dispose ();
			GC.SuppressFinalize (this);
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
	}
}

