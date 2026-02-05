using System;
using System.ComponentModel;
using System.Diagnostics;

namespace Android.Runtime
{
	[DebuggerBrowsable (DebuggerBrowsableState.Never)]
	[EditorBrowsable (EditorBrowsableState.Never)]
	public static class AndroidEnvironmentInternal
	{
		internal static Action<Exception>? UnhandledExceptionHandler;

		[DebuggerBrowsable (DebuggerBrowsableState.Never)]
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
