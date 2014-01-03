using System;

namespace Java.Interop
{
	static partial class JniHandles
	{
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

