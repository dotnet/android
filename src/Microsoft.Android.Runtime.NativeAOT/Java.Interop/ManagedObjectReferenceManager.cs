// Originally from: https://github.com/dotnet/java-interop/blob/dd3c1d0514addfe379f050627b3e97493e985da6/src/Java.Runtime.Environment/Java.Interop/ManagedObjectReferenceManager.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Java.Interop {

	class ManagedObjectReferenceManager : JniRuntime.JniObjectReferenceManager {

		TextWriter? grefLog;
		TextWriter? lrefLog;

		int         grefCount;
		int         wgrefCount;


		public  override    int     GlobalReferenceCount        => grefCount;
		public  override    int     WeakGlobalReferenceCount    => wgrefCount;

		public  override    bool    LogLocalReferenceMessages   => lrefLog != null;
		public  override    bool    LogGlobalReferenceMessages  => grefLog != null;

		public ManagedObjectReferenceManager (TextWriter? grefLog, TextWriter? lrefLog)
		{
			if (grefLog != null && lrefLog != null && object.ReferenceEquals (grefLog, lrefLog)) {
				this.grefLog = this.lrefLog = TextWriter.Synchronized (grefLog);
				return;
			}

			var grefPath    = Environment.GetEnvironmentVariable ("JAVA_INTEROP_GREF_LOG");
			var lrefPath    = Environment.GetEnvironmentVariable ("JAVA_INTEROP_LREF_LOG");

			bool samePath   = !string.IsNullOrEmpty (grefPath) &&
				!string.IsNullOrEmpty (lrefPath) &&
				grefPath == lrefPath;

			if (grefLog != null) {
				this.grefLog    = TextWriter.Synchronized (grefLog);
			}
			if (lrefLog != null) {
				this.lrefLog    = TextWriter.Synchronized (lrefLog);
			}

			if (this.grefLog == null && !string.IsNullOrEmpty (grefPath)) {
				this.grefLog    = TextWriter.Synchronized (CreateTextWriter (grefPath));
			}
			if (this.lrefLog == null && samePath) {
				this.lrefLog    = this.grefLog;
			}
			if (this.lrefLog == null && !string.IsNullOrEmpty (lrefPath)) {
				this.lrefLog    = TextWriter.Synchronized (CreateTextWriter (lrefPath));
			}
		}

		public override void OnSetRuntime (JniRuntime runtime)
		{
			base.OnSetRuntime (runtime);
		}

		static TextWriter CreateTextWriter (string path)
		{
			return new StreamWriter (path, append: false, encoding: new UTF8Encoding (encoderShouldEmitUTF8Identifier: false));
		}

		public override void WriteLocalReferenceLine (string format, params object[] args)
		{
			if (lrefLog == null)
				return;
			lrefLog.WriteLine (format, args);
			lrefLog.Flush ();
		}

		public override JniObjectReference CreateLocalReference (JniObjectReference reference, ref int localReferenceCount)
		{
			if (!reference.IsValid)
				return reference;

			var r = base.CreateLocalReference (reference, ref localReferenceCount);

			CreatedReference (lrefLog, "+l+ lrefc", localReferenceCount, reference, r, Runtime);

			return r;
		}

		public override void DeleteLocalReference (ref JniObjectReference reference, ref int localReferenceCount)
		{
			if (!reference.IsValid)
				return;

			var r = reference;

			base.DeleteLocalReference (ref reference, ref localReferenceCount);

			DeletedReference (lrefLog, "-l- lrefc", localReferenceCount, r, Runtime);
		}

		public override void CreatedLocalReference (JniObjectReference reference, ref int localReferenceCount)
		{
			if (!reference.IsValid)
				return;
			base.CreatedLocalReference (reference, ref localReferenceCount);
			CreatedReference (lrefLog, "+l+ lrefc", localReferenceCount, reference, Runtime);
		}

		public override IntPtr ReleaseLocalReference (ref JniObjectReference reference, ref int localReferenceCount)
		{
			if (!reference.IsValid)
				return IntPtr.Zero;
			var r = reference;
			var p = base.ReleaseLocalReference (ref reference, ref localReferenceCount);
			DeletedReference (lrefLog, "-l- lrefc", localReferenceCount, r, Runtime);
			return p;
		}

		public override void WriteGlobalReferenceLine (string format, params object?[]? args)
		{
			if (grefLog == null)
				return;
			grefLog.WriteLine (format, args!);
			grefLog.Flush ();
		}

		public override JniObjectReference CreateGlobalReference (JniObjectReference reference)
		{
			if (!reference.IsValid)
				return reference;
			var n   = base.CreateGlobalReference (reference);
			int c   = Interlocked.Increment (ref grefCount);
			CreatedReference (grefLog, "+g+ grefc", c, reference, n, Runtime);
			return n;
		}

		public override void DeleteGlobalReference (ref JniObjectReference reference)
		{
			if (!reference.IsValid)
				return;
			int c   = Interlocked.Decrement (ref grefCount);
			DeletedReference (grefLog, "-g- grefc", c, reference, Runtime);
			base.DeleteGlobalReference (ref reference);
		}

		public override JniObjectReference CreateWeakGlobalReference (JniObjectReference reference)
		{
			if (!reference.IsValid)
				return reference;
			var n   = base.CreateWeakGlobalReference (reference);

			int wc  = Interlocked.Increment (ref wgrefCount);
			int gc  = grefCount;
			if (grefLog != null) {
				string message  = $"+w+ grefc {gc} gwrefc {wc} obj-handle {reference.ToString ()} -> new-handle {n.ToString ()} " +
					$"from thread '{Runtime.GetCurrentManagedThreadName ()}'({Environment.CurrentManagedThreadId})" +
					Environment.NewLine +
					Runtime.GetCurrentManagedThreadStackTrace (skipFrames: 2, fNeedFileInfo: true);
				grefLog.WriteLine (message);
				grefLog.Flush ();
			}

			return n;
		}

		public override void DeleteWeakGlobalReference (ref JniObjectReference reference)
		{
			if (!reference.IsValid)
				return;

			int wc  = Interlocked.Decrement (ref wgrefCount);
			int gc  = grefCount;

			if (grefLog != null) {
				string message  = $"-w- grefc {gc} gwrefc {wc} handle {reference.ToString ()} " +
					$"from thread '{Runtime.GetCurrentManagedThreadName ()}'({Environment.CurrentManagedThreadId})" +
					Environment.NewLine +
					Runtime.GetCurrentManagedThreadStackTrace (skipFrames: 2, fNeedFileInfo: true);
				grefLog.WriteLine (message);
				grefLog.Flush ();
			}

			base.DeleteWeakGlobalReference (ref reference);
		}

		protected override void Dispose (bool disposing)
		{
		}

		static void CreatedReference (TextWriter? writer, string kind, int count, JniObjectReference reference, JniRuntime runtime)
		{
			if (writer == null)
				return;
			string message  = $"{kind} {count} handle {reference.ToString ()} " +
				$"from thread '{runtime.GetCurrentManagedThreadName ()}'({Environment.CurrentManagedThreadId})" +
				Environment.NewLine +
				runtime.GetCurrentManagedThreadStackTrace (skipFrames: 2, fNeedFileInfo: true);
			writer.WriteLine (message);
			writer.Flush ();
		}

		static void CreatedReference (TextWriter? writer, string kind, int count, JniObjectReference reference, JniObjectReference newReference, JniRuntime runtime)
		{
			if (writer == null)
				return;
			string message  = $"{kind} {count} obj-handle {reference.ToString ()} -> new-handle {newReference.ToString ()} " +
				$"from thread '{runtime.GetCurrentManagedThreadName ()}'({Environment.CurrentManagedThreadId})" +
				Environment.NewLine +
				runtime.GetCurrentManagedThreadStackTrace (skipFrames: 2, fNeedFileInfo: true);
			writer.WriteLine (message);
			writer.Flush ();
		}

		static void DeletedReference (TextWriter? writer, string kind, int count, JniObjectReference reference, JniRuntime runtime)
		{
			if (writer == null)
				return;
			string message  = $"{kind} {count} handle {reference.ToString ()} " +
				$"from thread '{runtime.GetCurrentManagedThreadName ()}'({Environment.CurrentManagedThreadId})" +
				Environment.NewLine +
				runtime.GetCurrentManagedThreadStackTrace (skipFrames: 2, fNeedFileInfo: true);
			writer.WriteLine (message);
			writer.Flush ();
		}
	}
}