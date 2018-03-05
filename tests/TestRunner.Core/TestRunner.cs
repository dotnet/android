using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using Android.Content;
using Android.OS;
using Android.Util;

namespace Xamarin.Android.UnitTests
{
	public abstract class TestRunner
	{
		public string LogTag { get; internal set; }
		public long InconclusiveTests { get; protected set; } = 0;
		public long FailedTests { get; protected set; } = 0;
		public long PassedTests { get; protected set; } = 0;
		public long SkippedTests { get; protected set; } = 0;
		public long ExecutedTests { get; protected set; } = 0;
		public long TotalTests { get; protected set; } = 0;
		public long FilteredTests { get; protected set; } = 0;
		public bool RunInParallel { get; set; } = false;
		public string TestsRootDirectory { get; set; }
		public Context Context { get; }
		public List<TestFailureInfo> FailureInfos { get; } = new List<TestFailureInfo> ();

		protected LogWriter Logger { get; }
		protected abstract string ResultsFileName { get; set; }

		protected TestRunner (Context context, LogWriter logger, Bundle bundle)
		{
			Context = context ?? throw new ArgumentNullException (nameof (context));
			Logger = logger ?? throw new ArgumentNullException (nameof (logger));
		}

		public abstract void Run (IList <TestAssemblyInfo> testAssemblies);
		public abstract string WriteResultsToFile ();

		protected void OnError (string message)
		{
			Logger.OnError (LogTag, message);
		}

		protected void OnWarning (string message)
		{
			Logger.OnWarning (LogTag, message);
		}

		protected void OnDebug (string message)
		{
			Logger.OnDebug (LogTag, message);
		}

		protected void OnDiagnostic (string message)
		{
			Logger.OnDiagnostic (LogTag, message);
		}

		protected void OnInfo (string message)
		{
			Logger.OnInfo (LogTag, message);
		}

		protected void OnAssemblyStart (Assembly asm)
		{
			Log.Info (LogTag, $"Start: {asm}");
		}

		protected void OnAssemblyFinish (Assembly asm)
		{
			Log.Info (LogTag, $"Finish: {asm}");
		}

		protected void LogFailureSummary ()
		{
			if (FailureInfos == null || FailureInfos.Count == 0)
				return;

			OnInfo ("Failed tests:");
			for (int i = 1; i <= FailureInfos.Count; i++) {
				TestFailureInfo info = FailureInfos [i - 1];
				if (info == null || !info.HasInfo)
					continue;

				OnInfo ($"{i}) {info.Message}");
			}
		}

		void AssertExecutionState (TestExecutionState state)
		{
			if (state == null)
				throw new ArgumentNullException (nameof (state));
		}

		protected virtual string GetResultsFilePath ()
		{
			if (String.IsNullOrEmpty (ResultsFileName))
				throw new InvalidOperationException ("Runner didn't specify a valid results file name");
			
			Java.IO.File resultsPathFile = null;
#if __ANDROID_19__
			if (((int)Build.VERSION.SdkInt) >= 19)
				resultsPathFile = Context.GetExternalFilesDir (global::Android.OS.Environment.DirectoryDocuments);
#endif
			bool usePathFile = resultsPathFile != null && resultsPathFile.Exists ();
			string resultsPath = usePathFile ? resultsPathFile.AbsolutePath : Path.Combine (Context.FilesDir.AbsolutePath, ".__override__");
			if (!usePathFile && !Directory.Exists (resultsPath))
				Directory.CreateDirectory (resultsPath);

			return Path.Combine (resultsPath, ResultsFileName);
		}
	}
}
