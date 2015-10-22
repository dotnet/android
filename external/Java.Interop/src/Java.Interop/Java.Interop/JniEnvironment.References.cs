using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{
	partial class JniEnvironment {

		static partial class References
		{
			public static void Dispose (ref JniObjectReference reference)
			{
				Dispose (ref reference, JniHandleOwnership.Transfer);
			}

			public static void Dispose (ref JniObjectReference reference, JniHandleOwnership transfer)
			{
				if (!reference.IsValid)
					return;

				switch (transfer) {
				case JniHandleOwnership.DoNotTransfer:
					break;
				case JniHandleOwnership.Transfer:
					switch (reference.Type) {
					case JniObjectReferenceType.Global:
						JniEnvironment.Current.JavaVM.JniObjectReferenceManager.DeleteGlobalReference (ref reference);
						break;
					case JniObjectReferenceType.Local:
						JniEnvironment.Current.JavaVM.JniObjectReferenceManager.DeleteLocalReference (JniEnvironment.Current, ref reference);
						break;
					case JniObjectReferenceType.WeakGlobal:
						JniEnvironment.Current.JavaVM.JniObjectReferenceManager.DeleteWeakGlobalReference (ref reference);
						break;
					default:
						throw new NotImplementedException ("Do not know how to dispose: " + reference.Type + ".");
					}
					reference.Invalidate ();
					break;
				default:
					throw new NotImplementedException ("Do not know how to transfer: " + transfer);
				}
			}

			public static int GetIdentityHashCode (JniObjectReference value)
			{
				return JniSystem.IdentityHashCode (value);
			}

			public static IntPtr NewReturnToJniRef (IJavaPeerable value)
			{
				if (value == null || !value.PeerReference.IsValid)
					return IntPtr.Zero;
				return NewReturnToJniRef (value.PeerReference);
			}

			public static IntPtr NewReturnToJniRef (JniObjectReference value)
			{
				if (!value.IsValid)
					return IntPtr.Zero;
				var l = value.NewLocalRef ();
				return JniEnvironment.Current.JavaVM.JniObjectReferenceManager.ReleaseLocalReference (JniEnvironment.Current, ref l);
			}
		}
	}
}

