using System;

namespace Java.Interop
{
	public class JavaObject : IJavaObject
	{
		static JniType _typeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _typeRef, "java/lang/Object");}
		}

		int keyHandle;

		~JavaObject ()
		{
			var  h          = SafeHandle;
			bool collected  = JniEnvironment.Current.JavaVM.TryGC (this, ref h);
			if (collected) {
				SafeHandle = null;
				JniEnvironment.Current.JavaVM.UnRegisterObject (keyHandle, this);
				Dispose (false);
			} else {
				SafeHandle = h;
				GC.ReRegisterForFinalize (this);
			}
		}

		public JniReferenceSafeHandle SafeHandle {get; private set;}

		public JavaObject (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
		{
			if (handle == null)
				throw new ArgumentNullException ("handle");
			if (handle.IsInvalid)
				throw new ArgumentException ("handle is invalid.", "handle");

			SafeHandle = handle.NewGlobalRef ();
			JniEnvironment.Handles.Dispose (handle, transfer);

			keyHandle = JniSystem.IdentityHashCode (SafeHandle);
			JniEnvironment.Current.JavaVM.RegisterObject (keyHandle, this);
		}

		static JniInstanceMethodID Object_ctor;
		static JniLocalReference _NewObject ()
		{
			TypeRef.GetCachedConstructor (ref Object_ctor, "()V");
			return TypeRef.NewObject (Object_ctor);
		}

		public JavaObject ()
			: this (_NewObject (), JniHandleOwnership.Transfer)
		{
		}

		public void Dispose ()
		{
			Dispose (true);
			JniEnvironment.Current.JavaVM.UnRegisterObject (keyHandle, this);
			SafeHandle.Dispose ();
			GC.SuppressFinalize (this);
		}

		protected virtual void Dispose (bool disposing)
		{
		}

		public override bool Equals (object obj)
		{
			if (object.ReferenceEquals (obj, this))
				return true;
			var o = obj as IJavaObject;
			if (o != null)
				return JniObject.IsSameInstance (SafeHandle, o.SafeHandle);
			return false;
		}

		static JniInstanceMethodID Object_hashCode;
		public override int GetHashCode ()
		{
			return TypeRef.GetCachedInstanceMethod (ref Object_hashCode, "hashCode", "()I")
				.CallVirtualInt32Method (SafeHandle);
		}

		static JniInstanceMethodID Object_toString;
		public override string ToString ()
		{
			TypeRef.GetCachedInstanceMethod (ref Object_toString, "toString", "()Ljava/lang/String;");
			var lref = Object_toString.CallVirtualObjectMethod (SafeHandle);
			return JniEnvironment.Strings.ToString (lref, JniHandleOwnership.Transfer);
		}
	}
}

