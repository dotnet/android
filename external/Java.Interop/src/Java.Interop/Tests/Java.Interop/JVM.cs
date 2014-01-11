using System;
using System.IO;
using System.Diagnostics;
using System.Linq;

using Java.Interop;

namespace Java.InteropTests {

	class JVM : JavaVM {

		public static readonly new JavaVM Current = new JVM ();

		TextWriter  grefLog;

		JVM ()
			: base (new JavaVMBuilder ().AddSystemProperty ("java.class.path", "interop-test.jar"))
		{
			string logGrefs = (Environment.GetEnvironmentVariable ("_JI_LOG") ?? "")
				.Split (new []{ ',' }, StringSplitOptions.RemoveEmptyEntries)
				.FirstOrDefault (e => e.StartsWith ("gref"));
			if (logGrefs != null) {
				if (logGrefs == "gref")
					grefLog = Console.Out;
				if (logGrefs.StartsWith ("gref=")) {
					string file = logGrefs.Substring ("gref=".Length);
					grefLog = File.CreateText (file);
				}
			}
		}

		protected override void LogCreateGlobalRef (JniGlobalReference value, JniReferenceSafeHandle sourceValue)
		{
			base.LogCreateGlobalRef (value, sourceValue);
			if (grefLog == null)
				return;
			grefLog.WriteLine ("+g+ grefc {0} gwrefc {1} obj-handle 0x{2}/{3} -> new-handle 0x{4}/{5} from {6}",
					GlobalReferenceCount,
					WeakGlobalReferenceCount,
					sourceValue.DangerousGetHandle ().ToString ("x"),
					ToChar (sourceValue.RefType),
					value.DangerousGetHandle ().ToString ("x"),
					ToChar (value.RefType),
					new StackTrace (true));
			grefLog.Flush ();
		}

		protected override void LogDestroyGlobalRef (IntPtr value)
		{
			base.LogDestroyGlobalRef (value);
			if (grefLog == null)
				return;
			grefLog.WriteLine ("-g- grefc {0} gwrefc {1} handle 0x{2}/{3} from {4}",
				GlobalReferenceCount,
				WeakGlobalReferenceCount,
				value.ToString ("x"),
				'G',
				new StackTrace (true));
			grefLog.Flush ();
		}

		protected override void LogCreateWeakGlobalRef (JniWeakGlobalReference value, JniReferenceSafeHandle sourceValue)
		{
			base.LogCreateWeakGlobalRef (value, sourceValue);
			if (grefLog == null)
				return;
			grefLog.WriteLine ("+w+ grefc {0} gwrefc {1} obj-handle 0x{2}/{3} -> new-handle 0x{4}/{5} from {6}",
				GlobalReferenceCount,
				WeakGlobalReferenceCount,
				sourceValue.DangerousGetHandle ().ToString ("x"),
				ToChar (sourceValue.RefType),
				value.DangerousGetHandle ().ToString ("x"),
				ToChar (value.RefType),
				new StackTrace (true));
			grefLog.Flush ();
		}

		protected override void LogDestroyWeakGlobalRef (IntPtr value)
		{
			base.LogDestroyWeakGlobalRef (value);
			if (grefLog == null)
				return;
			grefLog.WriteLine ("-w- grefc {0} gwrefc {1} handle 0x{2}/{3} from {4}",
				GlobalReferenceCount,
				WeakGlobalReferenceCount,
				value.ToString ("x"),
				'G',
				new StackTrace (true));
			grefLog.Flush ();
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

