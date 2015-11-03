using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{
	partial class JniEnvironment {

		static partial class References
		{
#if !XA_INTEGRATION
			public static int GetIdentityHashCode (JniObjectReference value)
			{
				return JniSystem.IdentityHashCode (value);
			}
#endif  // !XA_INTEGRATION

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
				return JniEnvironment.Runtime.ObjectReferenceManager.ReleaseLocalReference (JniEnvironment.CurrentInfo, ref l);
			}
		}
	}
}

