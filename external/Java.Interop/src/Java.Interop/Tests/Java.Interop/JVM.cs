using System;
using System.Diagnostics;

using Java.Interop;

namespace Java.InteropTests {

	class JVM : JavaVM {

		public static readonly new JavaVM Current = new JVM ();

		JVM ()
		{
		}

		protected override void LogCreateGlobalRef (JniGlobalReference value, JniReferenceSafeHandle sourceValue)
		{
			base.LogCreateGlobalRef (value, sourceValue);
			Console.WriteLine ("+g+ grefc {0} gwrefc {1} obj-handle 0x{2}/{3} -> new-handle {4}/{5} from {6}",
					GlobalReferenceCount,
					WeakGlobalReferenceCount,
					sourceValue.DangerousGetHandle ().ToString ("x"),
					ToChar (sourceValue.RefType),
					value.DangerousGetHandle ().ToString ("x"),
					ToChar (value.RefType),
					new StackTrace (true));
		}

		protected override void LogDestroyGlobalRef (IntPtr value)
		{
			base.LogDestroyGlobalRef (value);
			Console.WriteLine ("-g- grefc {0} gwrefc {1} handle 0x{2}/{3} from {4}",
				GlobalReferenceCount,
				WeakGlobalReferenceCount,
				value.ToString ("x"),
				'G',
				new StackTrace (true));
		}

		static char ToChar (JObjectRefType type)
		{
			switch (type) {
			case JObjectRefType.Global:     return 'G';
			case JObjectRefType.Invalid:    return 'I';
			case JObjectRefType.Local:      return 'L';
			case JObjectRefType.WeakGlobal: return 'W';
			}
			return '*';
		}
	}
}

