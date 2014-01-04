using System;
using System.Diagnostics;

using Java.Interop;

namespace Java.InteropTests {

	class JVM : JavaVM {

		public static readonly new JavaVM Current = new JVM ();

		JVM ()
		{
		}

		protected override void LogCreateGlobalRef (JniGlobalReference value)
		{
			base.LogCreateGlobalRef (value);
			Console.WriteLine ("+g+ grefc {0} gwrefc {1} new-handle 0x{2} from {3}",
					GlobalReferenceCount,
					WeakGlobalReferenceCount,
					value.DangerousGetHandle ().ToString ("x"),
					new StackTrace (true));
		}

		protected override void LogDestroyGlobalRef (IntPtr value)
		{
			base.LogDestroyGlobalRef (value);
			Console.WriteLine ("-g- grefc {0} gwrefc {1} handle 0x{2} from {3}",
				GlobalReferenceCount,
				WeakGlobalReferenceCount,
				value.ToString ("x"),
				new StackTrace (true));
		}
	}
}

