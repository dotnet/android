using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks
{
	public static class MSBuildExtensions
	{
		private static bool IsRunningInsideVS {
			get { 
				var vside = false;
				return bool.TryParse(Environment.GetEnvironmentVariable("VSIDE"), out vside) && vside; 
			}
		}

		public static void LogDebugMessage (this TaskLoggingHelper log, string message, params object[] messageArgs)
		{
			log.LogMessage (MessageImportance.Low, message, messageArgs);
		}

		public static void LogTaskItems (this TaskLoggingHelper log, string message, ITaskItem[] items)
		{
			log.LogMessage (message);

			if (items == null)
				return;

			foreach (var item in items)
				log.LogMessage ("    {0}", item.ItemSpec);
		}

		public static void LogTaskItems (this TaskLoggingHelper log, string message, params string[] items)
		{
			log.LogMessage (message);

			if (items == null)
				return;

			foreach (var item in items)
				log.LogMessage ("    {0}", item);
		}

		public static void LogDebugTaskItems (this TaskLoggingHelper log, string message, ITaskItem[] items, bool logMetadata = false)
		{
			log.LogMessage (MessageImportance.Low, message);

			if (items == null)
				return;

			foreach (var item in items) {
				log.LogMessage (MessageImportance.Low, "    {0}", item.ItemSpec);
				if (!logMetadata || item.MetadataCount <= 0)
					continue;
				foreach (string name in item.MetadataNames)
					log.LogMessage (MessageImportance.Low, $"       {name} = {item.GetMetadata (name)}");
			}
		}

		public static void LogDebugTaskItems (this TaskLoggingHelper log, string message, params string[] items)
		{
			log.LogMessage (MessageImportance.Low, message);

			if (items == null)
				return;

			foreach (var item in items)
				log.LogMessage (MessageImportance.Low, "    {0}", item);
		}

		// looking for: mandroid: warning XA9000: message...
		static readonly Regex Message = new Regex (
				@"^(?<source>[^: ]+)\s*:\s*(?<type>warning|error) (?<code>[^:]+): (?<message>.*)");

		public static void LogFromStandardError (this TaskLoggingHelper log, string defaultErrorCode, string message)
		{
			if (string.IsNullOrEmpty (message))
				return;

			var m = Message.Match (message);
			if (!m.Success) {
				if (message.IndexOf ("error:", StringComparison.InvariantCultureIgnoreCase) != -1) {
					log.LogCodedError (defaultErrorCode, message);
				} else {
					log.LogMessage (null, defaultErrorCode, null, null, 0, 0, 0, 0, MessageImportance.Low, message);
				}
				return;
			}

			string subcategory  = m.Groups ["source"].Value;
			string type         = m.Groups ["type"].Value;
			string code         = m.Groups ["code"].Value;
			string msg          = m.Groups ["message"].Value;

			if (string.IsNullOrEmpty (code))
				code = defaultErrorCode;

			if (type == "warning")
				log.LogWarning (subcategory, code, string.Empty, string.Empty, 0, 0, 0, 0, "{0}", msg);
			else
				log.LogError (subcategory, code, string.Empty, string.Empty, 0, 0, 0, 0, "{0}", msg);
		}

		public static void LogDebugTaskItemsAndLogical (this TaskLoggingHelper log, string message, ITaskItem[] items)
		{
			log.LogMessage (MessageImportance.Low, message);

			if (items == null)
				return;

			foreach (var item in items) {
				log.LogMessage (MessageImportance.Low, "    {0}", item.ItemSpec);
				log.LogMessage (MessageImportance.Low, "      [{0}]", item.GetMetadata ("LogicalName"));
			}
		}

		public static void LogCodedError (this TaskLoggingHelper log, string code, string message, params object[] messageArgs)
		{
			log.LogError (string.Empty, code, string.Empty, string.Empty, 0, 0, 0, 0, message, messageArgs);
		}

		public static void LogCodedError (this TaskLoggingHelper log, string code, string file, int lineNumber, string message, params object[] messageArgs)
		{
			log.LogError (string.Empty, code, string.Empty, file, lineNumber, 0, 0, 0, message, messageArgs);
		}

		public static void LogCodedWarning (this TaskLoggingHelper log, string code, string message, params object [] messageArgs)
		{
			log.LogWarning (string.Empty, code, string.Empty, string.Empty, 0, 0, 0, 0, message, messageArgs);
		}

		public static void LogCodedWarning (this TaskLoggingHelper log, string code, string file, int lineNumber, string message, params object [] messageArgs)
		{
			log.LogWarning (string.Empty, code, string.Empty, file, lineNumber, 0, 0, 0, message, messageArgs);
		}

		public static Action<TraceLevel, string> CreateTaskLogger (this Task task)
		{
			Action<TraceLevel, string> logger = (level, value) => {
				switch (level) {
				case TraceLevel.Error:
					task.Log.LogError ("{0}", value);
					break;
				case TraceLevel.Warning:
					task.Log.LogWarning ("{0}", value);
					break;
				default:
					task.Log.LogDebugMessage ("{0}", value);
					break;
				}
			};
			return logger;
		}

		public static Action<TraceLevel, string> CreateTaskLogger (this AsyncTask task)
		{
			Action<TraceLevel, string> logger = (level, value) => {
				switch (level) {
				case TraceLevel.Error:
					task.LogError (value);
					break;
				case TraceLevel.Warning:
					task.LogWarning (value);
					break;
				default:
					task.LogDebugMessage (value);
					break;
				}
			};
			return logger;
		}


		public static IEnumerable<ITaskItem> Concat (params ITaskItem[][] values)
		{
			if (values == null)
				yield break;
			foreach (ITaskItem[] v in values) {
				if (v == null)
					continue;
				foreach (ITaskItem t in v)
					yield return t;
			}
		}

		public static string FixupResourceFilename (string file, string resourceDir, Dictionary<string, string> resourceNameCaseMap)
		{
			var targetfile = file;
			if (resourceDir != null && targetfile.StartsWith (resourceDir, StringComparison.InvariantCultureIgnoreCase)) {
				targetfile = file.Substring (resourceDir.Length).TrimStart (Path.DirectorySeparatorChar);
				if (resourceNameCaseMap.TryGetValue (targetfile, out string temp))
					targetfile = temp;
				targetfile = Path.Combine ("Resources", targetfile);
			}
			return targetfile;
		}

		public static void FixupResourceFilenameAndLogCodedError (this TaskLoggingHelper log, string code, string message, string file,  string resourceDir, Dictionary<string, string> resourceNameCaseMap)
		{
			var targetfile = FixupResourceFilename (file, resourceDir, resourceNameCaseMap);
			log.LogCodedError (code, file: targetfile, lineNumber: 0, message: message);
		}

		public static void FixupResourceFilenameAndLogCodedWarning (this TaskLoggingHelper log, string code, string message, string file, string resourceDir, Dictionary<string, string> resourceNameCaseMap)
		{
			var targetfile = FixupResourceFilename (file, resourceDir, resourceNameCaseMap);
			log.LogCodedWarning (code, file: targetfile, lineNumber: 0, message: message);
		}
	}
}
