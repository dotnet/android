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
			if (handle == null)
				return;
			using (SetSafeHandle (handle, transfer)) {
			}
		}

		public unsafe JavaObject ()
		{
			using (SetSafeHandle (
						JniPeerMembers.InstanceMethods.StartCreateInstance ("()V", GetType (), null),
						JniHandleOwnership.Transfer)) {
				JniPeerMembers.InstanceMethods.FinishCreateInstance ("()V", this, null);
			}
		}

		protected SetSafeHandleCompletion SetSafeHandle (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
		{
			return JniEnvironment.Current.JavaVM.SetObjectSafeHandle (
					this,
					handle,
					transfer,
					a => new SetSafeHandleCompletion (a));
		}

		public void RegisterWithVM ()
		{
			JniEnvironment.Current.JavaVM.RegisterObject (this);
		}

		public void Dispose ()
		{
			JniEnvironment.Current.JavaVM.DisposeObject (this);
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

		public override unsafe int GetHashCode ()
		{
			return _members.InstanceMethods.CallInt32Method ("hashCode\u0000()I", this, null);
		}

		public override unsafe string ToString ()
		{
			var lref = _members.InstanceMethods.CallObjectMethod (
					"toString\u0000()Ljava/lang/String;",
					this,
					null);
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

