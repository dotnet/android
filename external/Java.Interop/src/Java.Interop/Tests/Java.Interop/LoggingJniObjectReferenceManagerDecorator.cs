using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

using Java.Interop;

namespace Java.InteropTests {

	class LoggingJniObjectReferenceManagerDecorator : IJniObjectReferenceManager {

		TextWriter          grefLog;
		TextWriter          lrefLog;

		IJniObjectReferenceManager      manager;

		public LoggingJniObjectReferenceManagerDecorator (IJniObjectReferenceManager manager, TextWriter lrefOutput = null, TextWriter grefOutput = null)
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

		public static IJniObjectReferenceManager GetObjectReferenceManager (IJniObjectReferenceManager manager)
		{
			TextWriter  grefLog = null;
			TextWriter  lrefLog = null;;
			var log = Environment.GetEnvironmentVariable ("_JI_LOG") ?? "";

			string logGrefs = log
				.Split (new []{ ',' }, StringSplitOptions.RemoveEmptyEntries)
				.FirstOrDefault (e => e.StartsWith ("gref"));
			string grefFile = GetLogPath (logGrefs);

			string logLrefs = log
				.Split (new []{ ',' }, StringSplitOptions.RemoveEmptyEntries)
				.FirstOrDefault (e => e.StartsWith ("lref"));
			string lrefFile = GetLogPath (logLrefs);
			if (logGrefs != null) {
				grefLog = GetWriter (grefFile, "grefs");
			}
			if (logLrefs != null) {
				if (!string.IsNullOrEmpty (grefFile) && grefFile == lrefFile)
					lrefLog = grefLog;
				else
					lrefLog = GetWriter (lrefFile, "lrefs");
			}

			if (grefLog == null && lrefLog == null)
				return manager;
			return new LoggingJniObjectReferenceManagerDecorator (manager, lrefLog, grefLog);
		}

		static string GetLogPath (string value)
		{
			if (value == null)
				return null;
			int i = value.IndexOf ('=');
			if (i <= 0)
				return null;
			return value.Substring (i + 1);
		}

		static TextWriter GetWriter (string path, string category)
		{
#if __ANDROID__
			var root    = Environment.GetFolderPath (Environment.SpecialFolder.Personal);
			if (path == null)
				path = Path.Combine (root, "ji-" + category + ".txt");
			else
				path = Path.Combine (root, path);
			Console.WriteLine ("# Writing Java.Interop '{0}' messages to '{1}'.", category, path);
			return File.CreateText (path);
#else
			if (path == null)
				return Console.Out;
			return File.CreateText (path);
#endif
		}

		public void WriteLocalReferenceLine (string format, params object[] args)
		{
			if (lrefLog == null)
				return;
			var message = string.Format (format, args);
			lock (lrefLog) {
				lrefLog.WriteLine (message);
				lrefLog.Flush ();
			}
		}

		public JniObjectReference CreateLocalReference (JniEnvironment environment, JniObjectReference value)
		{
			var newValue    = manager.CreateLocalReference (environment, value);
			if (lrefLog == null || !newValue.IsValid)
				return newValue;
			var t = Thread.CurrentThread;
			LogLref ("+l+ lrefc {0} obj-handle 0x{1}/{2} -> new-handle 0x{3}/{4} from thread '{5}'({6}){7}{8}",
					environment.LocalReferenceCount,
					value.Handle.ToString ("x"),
					ToChar (value.Type),
					newValue.Handle.ToString ("x"),
					ToChar (newValue.Type),
					t.Name,
					t.ManagedThreadId,
					Environment.NewLine,
					new StackTrace (true));
			return newValue;
		}

		public void DeleteLocalReference (JniEnvironment environment, ref JniObjectReference value)
		{
			if (lrefLog != null && value.IsValid) {
				LogDeleteLocalRef (environment, value.Handle);
			}
			manager.DeleteLocalReference (environment, ref value);
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

		public void CreatedLocalReference (JniEnvironment environment, JniObjectReference value)
		{
			manager.CreatedLocalReference (environment, value);
			if (lrefLog == null || !value.IsValid)
				return;
			var t = Thread.CurrentThread;
			LogLref ("+l+ lrefc {0} -> new-handle 0x{1}/{2} from thread '{3}'({4}){5}{6}",
					environment.LocalReferenceCount,
					value.Handle.ToString ("x"),
					ToChar (value.Type),
					t.Name,
					t.ManagedThreadId,
					Environment.NewLine,
					new StackTrace (true));
		}

		public IntPtr ReleaseLocalReference (JniEnvironment environment, ref JniObjectReference value)
		{
			if (lrefLog != null && value.IsValid) {
				LogDeleteLocalRef (environment, value.Handle);
			}
			return manager.ReleaseLocalReference (environment, ref value);
		}

		public void WriteGlobalReferenceLine (string format, params object[] args)
		{
			if (grefLog == null)
				return;
			var message = string.Format (format, args);
			lock (grefLog) {
				grefLog.WriteLine (message);
				grefLog.Flush ();
			}
		}

		public JniObjectReference CreateGlobalReference (JniObjectReference value)
		{
			var newValue    = manager.CreateGlobalReference (value);
			if (grefLog == null || !newValue.IsValid)
				return newValue;
			var t = Thread.CurrentThread;
			LogGref ("+g+ grefc {0} gwrefc {1} obj-handle 0x{2}/{3} -> new-handle 0x{4}/{5} from thread '{6}'({7}){8}{9}",
					GlobalReferenceCount,
					WeakGlobalReferenceCount,
					value.Handle.ToString ("x"),
					ToChar (value.Type),
					newValue.Handle.ToString ("x"),
					ToChar (newValue.Type),
					t.Name,
					t.ManagedThreadId,
					Environment.NewLine,
					new StackTrace (true));
			return newValue;
		}

		public void DeleteGlobalReference (ref JniObjectReference value)
		{
			if (grefLog != null && value.IsValid) {
				var t = Thread.CurrentThread;
				LogGref ("-g- grefc {0} gwrefc {1} handle 0x{2}/{3} from thread '{4}'({5}){6}{7}",
						GlobalReferenceCount,
						WeakGlobalReferenceCount,
						value.Handle.ToString ("x"),
						'G',
						t.Name,
						t.ManagedThreadId,
						Environment.NewLine,
						new StackTrace (true));
			}
			manager.DeleteGlobalReference (ref value);
		}

		public JniObjectReference CreateWeakGlobalReference (JniObjectReference value)
		{
			var newValue    = manager.CreateWeakGlobalReference (value);
			if (grefLog == null || !newValue.IsValid) {
				return newValue;
			}
			var t = Thread.CurrentThread;
			LogGref ("+w+ grefc {0} gwrefc {1} obj-handle 0x{2}/{3} -> new-handle 0x{4}/{5} from thread '{6}'({7}){8}{9}",
					GlobalReferenceCount,
					WeakGlobalReferenceCount,
					value.Handle.ToString ("x"),
					ToChar (value.Type),
					newValue.Handle.ToString ("x"),
					ToChar (newValue.Type),
					t.Name,
					t.ManagedThreadId,
					Environment.NewLine,
					new StackTrace (true));
			return newValue;
		}

		public void DeleteWeakGlobalReference (ref JniObjectReference value)
		{
			if (grefLog != null && value.IsValid) {
				var t = Thread.CurrentThread;
				LogGref ("-w- grefc {0} gwrefc {1} handle 0x{2}/{3} from thread '{4}'({5}){6}{7}",
						GlobalReferenceCount,
						WeakGlobalReferenceCount,
						value.Handle.ToString ("x"),
						'W',
						t.Name,
						t.ManagedThreadId,
						Environment.NewLine,
						new StackTrace (true));
			}
			manager.DeleteWeakGlobalReference (ref value);
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

		static char ToChar (JniObjectReferenceType type)
		{
			switch (type) {
			case JniObjectReferenceType.Global:         return 'G';
			case JniObjectReferenceType.Invalid:        return 'I';
			case JniObjectReferenceType.Local:          return 'L';
			case JniObjectReferenceType.WeakGlobal:     return 'W';
			}
			return '*';
		}
	}
}

