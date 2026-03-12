using System;
using System.ComponentModel;

namespace Android.Runtime
{
	[EditorBrowsable (EditorBrowsableState.Never)]
	public static class AndroidEnvironmentInternal
	{
		internal static Action<Exception>? UnhandledExceptionHandler;

		[EditorBrowsable (EditorBrowsableState.Never)]
		public static void UnhandledException (Exception e)
		{
			if (UnhandledExceptionHandler == null) {
				return;
			}

			UnhandledExceptionHandler (e);
		}
	}
}
