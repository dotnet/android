using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	class EnvironmentFilesParser
	{
		public bool BrokenExceptionTransitions       { get; set; }
		public bool HavebuildId                      { get; private set; }
		public bool HaveHttpMessageHandler           { get; private set; }
		public bool HaveLogLevel                     { get; private set; }
		public bool HaveMonoDebug                    { get; private set; }
		public bool HaveMonoGCParams                 { get; private set; }
		public bool HaveTlsProvider                  { get; private set; }
		public bool UsesAssemblyPreload              { get; set; }
		public List<string> EnvironmentVariableLines { get; } = new List<string> ();

		public bool AreBrokenExceptionTransitionsEnabled (ITaskItem[] environments)
		{
			foreach (ITaskItem env in environments ?? Array.Empty<ITaskItem> ()) {
				foreach (string line in File.ReadLines (env.ItemSpec)) {
					if (IsBrokenExceptionTransitionsLine (line.Trim ())) {
						return true;
					}
				}
			}

			return false;
		}

		public void Parse (ITaskItem[] environments, SequencePointsMode sequencePointsMode, TaskLoggingHelper log)
		{
			foreach (ITaskItem env in environments ?? Array.Empty<ITaskItem> ()) {
				foreach (string line in File.ReadLines (env.ItemSpec)) {
					var lineToWrite = line.Trim ();
					if (lineToWrite.StartsWith ("MONO_LOG_LEVEL=", StringComparison.Ordinal))
						HaveLogLevel = true;
					if (lineToWrite.StartsWith ("MONO_GC_PARAMS=", StringComparison.Ordinal)) {
						HaveMonoGCParams = true;
						if (lineToWrite.IndexOf ("bridge-implementation=old", StringComparison.Ordinal) >= 0) {
							log.LogCodedWarning ("XA2000", Properties.Resources.XA2000_gcParams_bridgeImpl);
						}
					}
					if (lineToWrite.StartsWith ("XAMARIN_BUILD_ID=", StringComparison.Ordinal))
						HavebuildId = true;
					if (lineToWrite.StartsWith ("MONO_DEBUG=", StringComparison.Ordinal)) {
						HaveMonoDebug = true;
						if (sequencePointsMode != SequencePointsMode.None && !lineToWrite.Contains ("gen-compact-seq-points"))
							lineToWrite = line  + ",gen-compact-seq-points";
					}
					if (lineToWrite.StartsWith ("XA_HTTP_CLIENT_HANDLER_TYPE=", StringComparison.Ordinal))
						HaveHttpMessageHandler = true;

					if (lineToWrite.StartsWith ("mono.enable_assembly_preload=", StringComparison.Ordinal)) {
						int idx = lineToWrite.IndexOf ('=');
						uint val;
						if (idx < lineToWrite.Length - 1 && UInt32.TryParse (lineToWrite.Substring (idx + 1), out val)) {
							UsesAssemblyPreload = idx == 1;
						}
						continue;
					}
					if (IsBrokenExceptionTransitionsLine (lineToWrite)) {
						BrokenExceptionTransitions = true;
						continue;
					}

					EnvironmentVariableLines.Add (lineToWrite);
				}
			}
		}

		bool IsBrokenExceptionTransitionsLine (string lineToWrite) => lineToWrite.StartsWith ("XA_BROKEN_EXCEPTION_TRANSITIONS=", StringComparison.Ordinal);
	}
}
