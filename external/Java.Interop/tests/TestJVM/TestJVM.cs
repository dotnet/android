using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using Java.Interop;

namespace Java.InteropTests
{
	public class TestJVM : JreVM {

		TextWriter  grefLog;
		TextWriter  lrefLog;

		static JreVMBuilder CreateBuilder (string[] jars)
		{
			var builder = new JreVMBuilder ();
			var _jars   = new List<string> (jars) {
				"java-interop.jar",
			};
			_jars.AddRange (jars);
			builder.AddSystemProperty ("java.class.path", string.Join (":", _jars));
			return builder;
		}

		public TestJVM (params string[] jars)
			: base (CreateBuilder (jars))
		{
			var log = Environment.GetEnvironmentVariable ("_JI_LOG") ?? "";
			string logGrefs = log
				.Split (new []{ ',' }, StringSplitOptions.RemoveEmptyEntries)
				.FirstOrDefault (e => e.StartsWith ("gref"));
			string logLrefs = log
				.Split (new []{ ',' }, StringSplitOptions.RemoveEmptyEntries)
				.FirstOrDefault (e => e.StartsWith ("lref"));
			if (logGrefs != null) {
				if (logGrefs == "gref")
					grefLog = Console.Out;
				if (logGrefs.StartsWith ("gref=")) {
					string file = logGrefs.Substring ("gref=".Length);
					grefLog = File.CreateText (file);
				}
			}
			if (logLrefs != null) {
				if (logGrefs == "lref")
					lrefLog = Console.Out;
				if (logLrefs.StartsWith ("lref=")) {
					string file = logLrefs.Substring ("lref=".Length);
					lrefLog = File.CreateText (file);
				}
			}
		}

		protected override void LogCreateLocalRef (JniEnvironmentSafeHandle environmentHandle, JniLocalReference value)
		{
			base.LogCreateLocalRef (environmentHandle, value);
			if (lrefLog == null)
				return;
			var t = Thread.CurrentThread;
			LogLref ("+l+ thread '{0}'({1}) lrefc {2} -> new-handle 0x{3}/{4} from{5}{6}",
					t.Name,
					t.ManagedThreadId,
					JniEnvironment.Current.LocalReferenceCount,
					value.DangerousGetHandle ().ToString ("x"),
					ToChar (value.ReferenceType),
					Environment.NewLine,
					new StackTrace (true));
		}

		void LogLref (string format, params object[] args)
		{
			var message = string.Format (format, args);
			lock (lrefLog) {
				lrefLog.WriteLine (message);
				lrefLog.Flush ();
			}
		}

		protected override void LogCreateLocalRef (JniEnvironmentSafeHandle environmentHandle, JniLocalReference value, JniReferenceSafeHandle sourceValue)
		{
			base.LogCreateLocalRef (environmentHandle, value, sourceValue);
			if (grefLog == null)
				return;
			var t = Thread.CurrentThread;
			LogLref ("+l+ thread '{0}'({1}) lrefc {2} obj-handle 0x{3}/{4} -> new-handle 0x{5}/{6} from{7}{8}",
					t.Name,
					t.ManagedThreadId,
					JniEnvironment.Current.LocalReferenceCount,
					sourceValue.DangerousGetHandle ().ToString ("x"),
					ToChar (sourceValue.ReferenceType),
					value.DangerousGetHandle ().ToString ("x"),
					ToChar (value.ReferenceType),
					Environment.NewLine,
					new StackTrace (true));
		}

		protected override void LogDestroyLocalRef (JniEnvironmentSafeHandle environmentHandle, IntPtr value)
		{
			base.LogDestroyLocalRef (environmentHandle, value);
			if (lrefLog == null)
				return;
			var t = Thread.CurrentThread;
			LogLref ("-l- thread '{0}'({1}) lrefc {2} handle 0x{3}/{4} from{5}{6}",
					t.Name,
					t.ManagedThreadId,
					JniEnvironment.Current.LocalReferenceCount,
					value.ToString ("x"),
					'L',
					Environment.NewLine,
					new StackTrace (true));
		}

		protected override void LogCreateGlobalRef (JniGlobalReference value, JniReferenceSafeHandle sourceValue)
		{
			base.LogCreateGlobalRef (value, sourceValue);
			if (grefLog == null)
				return;
			LogGref ("+g+ grefc {0} gwrefc {1} obj-handle 0x{2}/{3} -> new-handle 0x{4}/{5} from {6}",
					GlobalReferenceCount,
					WeakGlobalReferenceCount,
					sourceValue.DangerousGetHandle ().ToString ("x"),
					ToChar (sourceValue.ReferenceType),
					value.DangerousGetHandle ().ToString ("x"),
					ToChar (value.ReferenceType),
					new StackTrace (true));
		}

		void LogGref (string format, params object[] args)
		{
			var message = string.Format (format, args);
			lock (grefLog) {
				grefLog.WriteLine (message);
				grefLog.Flush ();
			}
		}

		protected override void LogDestroyGlobalRef (IntPtr value)
		{
			base.LogDestroyGlobalRef (value);
			if (grefLog == null)
				return;
			LogGref ("-g- grefc {0} gwrefc {1} handle 0x{2}/{3} from {4}",
					GlobalReferenceCount,
					WeakGlobalReferenceCount,
					value.ToString ("x"),
					'G',
					new StackTrace (true));
		}

		protected override void LogCreateWeakGlobalRef (JniWeakGlobalReference value, JniReferenceSafeHandle sourceValue)
		{
			base.LogCreateWeakGlobalRef (value, sourceValue);
			if (grefLog == null)
				return;
			LogGref ("+w+ grefc {0} gwrefc {1} obj-handle 0x{2}/{3} -> new-handle 0x{4}/{5} from {6}",
					GlobalReferenceCount,
					WeakGlobalReferenceCount,
					sourceValue.DangerousGetHandle ().ToString ("x"),
					ToChar (sourceValue.ReferenceType),
					value.DangerousGetHandle ().ToString ("x"),
					ToChar (value.ReferenceType),
					new StackTrace (true));
		}

		protected override void LogDestroyWeakGlobalRef (IntPtr value)
		{
			base.LogDestroyWeakGlobalRef (value);
			if (grefLog == null)
				return;
			LogGref ("-w- grefc {0} gwrefc {1} handle 0x{2}/{3} from {4}",
					GlobalReferenceCount,
					WeakGlobalReferenceCount,
					value.ToString ("x"),
					'G',
					new StackTrace (true));
		}

		static char ToChar (JniReferenceType type)
		{
			switch (type) {
			case JniReferenceType.Global:       return 'G';
			case JniReferenceType.Invalid:      return 'I';
			case JniReferenceType.Local:        return 'L';
			case JniReferenceType.WeakGlobal:   return 'W';
			}
			return '*';
		}
	}
}

