using System;

namespace Java.Interop
{
	static partial class JniHandles
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
		public static JniGlobalReference NewGlobalRef (JniReferenceSafeHandle value)
		{
			// TODO: log
			return _NewGlobalRef (value);
		}
		/*
		public static void DeleteGlobalRef (JniGlobalReference value)
		{
			// TODO: log
			_DeleteGlobalRef (value);
		}
		*/

		public static JniLocalReference NewLocalRef (JniReferenceSafeHandle value)
		{
			// TODO: log
			return _NewLocalRef (value);
		}
		/*
		public static void DeleteLocalRef (JniLocalReference value)
		{
			// TODO: log
			_DeleteLocalRef (value);
		}
		*/
	}
}

