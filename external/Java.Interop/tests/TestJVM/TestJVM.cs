using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using Java.Interop;

namespace Java.InteropTests
{
	public class LoggingJniHandleManagerDecorator : IJniHandleManager {

		TextWriter          grefLog;
		TextWriter          lrefLog;
		IJniHandleManager   manager;

		public LoggingJniHandleManagerDecorator (IJniHandleManager manager, TextWriter lrefOutput = null, TextWriter grefOutput = null)
		{
			if (manager == null)
				throw new ArgumentNullException ("manager");

			this.manager    = manager;
			lrefLog         = lrefOutput;
			grefLog         = grefOutput;
		}

		public int GlobalReferenceCount {
			get {return manager.GlobalReferenceCount;}
		}

		public int WeakGlobalReferenceCount {
			get {return manager.WeakGlobalReferenceCount;}
		}

		public JniLocalReference CreateLocalReference (JniEnvironment environment, JniReferenceSafeHandle value)
		{
			var newValue    = manager.CreateLocalReference (environment, value);
			if (lrefLog == null || newValue == null || newValue.IsInvalid)
				return newValue;
			var t = Thread.CurrentThread;
			LogLref ("+l+ lrefc {0} obj-handle 0x{1}/{2} -> new-handle 0x{3}/{4} from thread '{5}'({6}){7}{8}",
					environment.LocalReferenceCount,
					value.DangerousGetHandle ().ToString ("x"),
					ToChar (value.ReferenceType),
					newValue.DangerousGetHandle ().ToString ("x"),
					ToChar (newValue.ReferenceType),
					t.Name,
					t.ManagedThreadId,
					Environment.NewLine,
					new StackTrace (true));
			return newValue;
		}

		public void DeleteLocalReference (JniEnvironment environment, IntPtr value)
		{
			if (lrefLog != null && value != IntPtr.Zero) {
				LogDeleteLocalRef (environment, value);
			}
			manager.DeleteLocalReference (environment, value);
		}

		void LogDeleteLocalRef (JniEnvironment environment, IntPtr value)
		{
			var t = Thread.CurrentThread;
			LogLref ("-l- lrefc {0} handle 0x{1}/{2} from thread '{3}'({4}){5}{6}",
					environment.LocalReferenceCount,
					value.ToString ("x"),
					'L',
					t.Name,
					t.ManagedThreadId,
					Environment.NewLine,
					new StackTrace (true));
		}

		public void CreatedLocalReference (JniEnvironment environment, JniLocalReference value)
		{
			manager.CreatedLocalReference (environment, value);
			if (lrefLog == null || value == null || value.IsInvalid)
				return;
			var t = Thread.CurrentThread;
			LogLref ("+l+ lrefc {0} -> new-handle 0x{1}/{2} from thread '{3}'({4}){5}{6}",
					environment.LocalReferenceCount,
					value.DangerousGetHandle ().ToString ("x"),
					ToChar (value.ReferenceType),
					t.Name,
					t.ManagedThreadId,
					Environment.NewLine,
					new StackTrace (true));
		}

		public IntPtr ReleaseLocalReference (JniEnvironment environment, JniLocalReference value)
		{
			if (lrefLog != null && value != null && !value.IsInvalid) {
				LogDeleteLocalRef (environment, value.DangerousGetHandle ());
			}
			return manager.ReleaseLocalReference (environment, value);
		}

		public JniGlobalReference CreateGlobalReference (JniReferenceSafeHandle value)
		{
			var newValue    = manager.CreateGlobalReference (value);
			if (grefLog == null || newValue == null || newValue.IsInvalid)
				return newValue;
			var t = Thread.CurrentThread;
			LogGref ("+g+ grefc {0} gwrefc {1} obj-handle 0x{2}/{3} -> new-handle 0x{4}/{5} from thread '{6}'({7}){8}{9}",
					GlobalReferenceCount,
					WeakGlobalReferenceCount,
					value.DangerousGetHandle ().ToString ("x"),
					ToChar (value.ReferenceType),
					newValue.DangerousGetHandle ().ToString ("x"),
					ToChar (newValue.ReferenceType),
					t.Name,
					t.ManagedThreadId,
					Environment.NewLine,
					new StackTrace (true));
			return newValue;
		}

		public void DeleteGlobalReference (IntPtr value)
		{
			if (grefLog != null && value != IntPtr.Zero) {
				var t = Thread.CurrentThread;
				LogGref ("-g- grefc {0} gwrefc {1} handle 0x{2}/{3} from thread '{4}'({5}){6}{7}",
						GlobalReferenceCount,
						WeakGlobalReferenceCount,
						value.ToString ("x"),
						'G',
						t.Name,
						t.ManagedThreadId,
						Environment.NewLine,
						new StackTrace (true));
			}
			manager.DeleteGlobalReference (value);
		}

		public JniWeakGlobalReference CreateWeakGlobalReference (JniReferenceSafeHandle value)
		{
			var newValue    = manager.CreateWeakGlobalReference (value);
			if (grefLog == null || newValue != null || newValue.IsInvalid) {
				return newValue;
			}
			var t = Thread.CurrentThread;
			LogGref ("+w+ grefc {0} gwrefc {1} obj-handle 0x{2}/{3} -> new-handle 0x{4}/{5} from thread '{6}'({7}){8}{9}",
					GlobalReferenceCount,
					WeakGlobalReferenceCount,
					value.DangerousGetHandle ().ToString ("x"),
					ToChar (value.ReferenceType),
					newValue.DangerousGetHandle ().ToString ("x"),
					ToChar (newValue.ReferenceType),
					t.Name,
					t.ManagedThreadId,
					Environment.NewLine,
					new StackTrace (true));
			return newValue;
		}

		public void DeleteWeakGlobalReference (IntPtr value)
		{
			if (grefLog != null && value != IntPtr.Zero) {
				var t = Thread.CurrentThread;
				LogGref ("-w- grefc {0} gwrefc {1} handle 0x{2}/{3} from thread '{4}'({5}){6}{7}",
						GlobalReferenceCount,
						WeakGlobalReferenceCount,
						value.ToString ("x"),
						'W',
						t.Name,
						t.ManagedThreadId,
						Environment.NewLine,
						new StackTrace (true));
			}
			manager.DeleteWeakGlobalReference (value);
		}

		public void Dispose ()
		{
			if (lrefLog != null)
				lrefLog.Dispose ();
			if (grefLog != null)
				grefLog.Dispose ();
			if (manager != null)
				manager.Dispose ();

			lrefLog = null;
			grefLog = null;
			manager = null;
		}

		void LogLref (string format, params object[] args)
		{
			var message = string.Format (format, args);
			lock (lrefLog) {
				lrefLog.WriteLine (message);
				lrefLog.Flush ();
			}
		}

		void LogGref (string format, params object[] args)
		{
			var message = string.Format (format, args);
			lock (grefLog) {
				grefLog.WriteLine (message);
				grefLog.Flush ();
			}
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

	public class TestJVM : JreVM {

		static JreVMBuilder CreateBuilder (string[] jars)
		{
			var builder = new JreVMBuilder ();
			var _jars   = new List<string> (jars ?? new string [0]) {
				"java-interop.jar",
			};
			_jars.AddRange (jars);
			builder.AddSystemProperty ("java.class.path", string.Join (":", _jars));

			TextWriter  grefLog = null;
			TextWriter  lrefLog = null;;
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
			builder.JniHandleManager = new LoggingJniHandleManagerDecorator (new Java.Interop.JniHandleManager (), lrefLog, grefLog);

			return builder;
		}

		Dictionary<string, Type> typeMappings;

		public TestJVM (string[] jars = null, Dictionary<string, Type> typeMappings = null)
			: base (CreateBuilder (jars))
		{
			this.typeMappings = typeMappings;
		}

		public override Type GetTypeForJniSimplifiedTypeReference (string jniTypeReference)
		{
			Type target = base.GetTypeForJniSimplifiedTypeReference (jniTypeReference);
			if (target != null)
				return target;
			if (typeMappings != null && typeMappings.TryGetValue (jniTypeReference, out target))
				return target;
			return null;
		}
	}
}

