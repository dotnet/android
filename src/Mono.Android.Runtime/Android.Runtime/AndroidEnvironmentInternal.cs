using System;

namespace Android.Runtime
{
	internal static class AndroidEnvironmentInternal
	{
		internal static Action<Exception>? UnhandledExceptionHandler;

		public static void UnhandledException (Exception e)
		{
			if (UnhandledExceptionHandler == null) {
				return;
			}

			UnhandledExceptionHandler (e);
		}
	}
}
