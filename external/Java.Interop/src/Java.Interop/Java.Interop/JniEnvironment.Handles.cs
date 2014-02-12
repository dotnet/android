using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{
	partial class JniEnvironment {

		static partial class Handles
		{
			internal static void Dispose (JniReferenceSafeHandle value, JniHandleOwnership transfer)
			{
				switch (transfer) {
				case JniHandleOwnership.DoNotTransfer:
					break;
				case JniHandleOwnership.Transfer:
					value.Dispose ();
					break;
				default:
					throw new NotImplementedException ("Do not know how to transfer: " + transfer);
				}
			}

			public static int GetIdentityHashCode (JniReferenceSafeHandle value)
			{
				return JniSystem.IdentityHashCode (value);
			}

			public static IntPtr NewReturnToJniRef (IJavaObject value)
			{
				if (value == null)
					return IntPtr.Zero;
				return NewReturnToJniRef (value.SafeHandle);
			}

			public static IntPtr NewReturnToJniRef (JniReferenceSafeHandle value)
			{
				if (value == null || value.IsInvalid)
					return IntPtr.Zero;
				using (var l = value.NewLocalRef ())
					return l.ReturnToJniRef ();
			}
		}
	}
}

