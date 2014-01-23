using System;

namespace Java.Interop
{
	partial class JniEnvironment {

		static partial class Handles
		{
			public static void Dispose (JniReferenceSafeHandle value, JniHandleOwnership transfer)
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
		}
	}
}

