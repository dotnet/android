// https://github.com/xamarin/xamarin-android/blob/eed430e4dc442ee98046fb13956ef49f29ce7b40/src/Xamarin.Android.Build.Tasks/Utilities/MSBuildExtensions.cs

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Text.RegularExpressions;

using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Xamarin.Build;

namespace Microsoft.Android.Build.Tasks
{
	[Flags]
	public enum RegisterTaskObjectKeyFlags {
		None = 0,
		IncludeProjectFile = 1 << 0,
	}

	public static class MSBuildExtensions
	{
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

		/// <summary>
		/// Logs a coded warning from a node in an XML document
		/// </summary>
		/// <param name="node">An element that implements IXmlLineInfo</param>
		public static void LogWarningForXmlNode (this TaskLoggingHelper log, string code, string file, object node, string message, params object [] messageArgs)
		{
			int lineNumber = 0;
			int columnNumber = 0;
			var lineInfo = node as IXmlLineInfo;
			if (lineInfo != null && lineInfo.HasLineInfo ()) {
				lineNumber = lineInfo.LineNumber;
				columnNumber = lineInfo.LinePosition;
			}
			log.LogWarning (
					subcategory: string.Empty,
					warningCode: code,
					helpKeyword: string.Empty,
					file: file,
					lineNumber: lineNumber,
					columnNumber: columnNumber,
					endLineNumber: 0,
					endColumnNumber: 0,
					message: message,
					messageArgs: messageArgs
			);
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

		/// <summary>
		/// Sets the default value for %(DestinationSubPath) if it is not already set
		/// </summary>
		public static void SetDestinationSubPath (this ITaskItem assembly)
		{
			if (string.IsNullOrEmpty (assembly.GetMetadata ("DestinationSubPath"))) {
				var directory = assembly.GetMetadata ("DestinationSubDirectory");
				var path = Path.Combine (directory, Path.GetFileName (assembly.ItemSpec));
				assembly.SetMetadata ("DestinationSubPath", path);
			}
		}

		static readonly string AssemblyLocation = typeof (MSBuildExtensions).Assembly.Location;

		/// <summary>
		/// IBuildEngine4.RegisterTaskObject, but adds the current assembly path into the key
		/// </summary>
		[Obsolete ("Use RegisterTaskObjectAssemblyLocal (engine, key, value, allowEarlyCollection, lifetime, flags) instead.")]
		public static void RegisterTaskObjectAssemblyLocal (this IBuildEngine4 engine, object key, object value, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection = false) =>
			RegisterTaskObjectAssemblyLocal (engine, key, value, lifetime, allowEarlyCollection: false, flags: RegisterTaskObjectKeyFlags.IncludeProjectFile);

		/// <summary>
		/// IBuildEngine4.RegisterTaskObject, but adds the current assembly path into the key
		/// </summary>
		public static void RegisterTaskObjectAssemblyLocal (this IBuildEngine4 engine, object key, object value, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection = false, RegisterTaskObjectKeyFlags flags = RegisterTaskObjectKeyFlags.IncludeProjectFile) =>
			engine.RegisterTaskObject (engine.GetKey (AssemblyLocation, key, flags), value, lifetime, allowEarlyCollection);

		/// <summary>
		/// IBuildEngine4.GetRegisteredTaskObject, but adds the current assembly path into the key
		/// </summary>
		[Obsolete ("Use GetRegisteredTaskObjectAssemblyLocal (engine, key, lifetime, flags) instead.")]
		public static object GetRegisteredTaskObjectAssemblyLocal (this IBuildEngine4 engine, object key, RegisteredTaskObjectLifetime lifetime) =>
			GetRegisteredTaskObjectAssemblyLocal (engine, key, lifetime, flags: RegisterTaskObjectKeyFlags.IncludeProjectFile);

		/// <summary>
		/// IBuildEngine4.GetRegisteredTaskObject, but adds the current assembly path into the key
		/// </summary>
		public static object GetRegisteredTaskObjectAssemblyLocal (this IBuildEngine4 engine, object key, RegisteredTaskObjectLifetime lifetime, RegisterTaskObjectKeyFlags flags = RegisterTaskObjectKeyFlags.IncludeProjectFile) =>
			engine.GetRegisteredTaskObject (engine.GetKey (AssemblyLocation, key, flags), lifetime);


		/// <summary>
		/// Generic version of IBuildEngine4.GetRegisteredTaskObject, but adds the current assembly path into the key
		/// </summary>
		[Obsolete ("Use GetRegisteredTaskObjectAssemblyLocal<T> (engine, key, lifetime, flags) instead.")]
		public static T GetRegisteredTaskObjectAssemblyLocal<T> (this IBuildEngine4 engine, object key, RegisteredTaskObjectLifetime lifetime)
			where T : class => GetRegisteredTaskObjectAssemblyLocal<T> (engine, key, lifetime, flags: RegisterTaskObjectKeyFlags.IncludeProjectFile);

		/// <summary>
		/// Generic version of IBuildEngine4.GetRegisteredTaskObject, but adds the current assembly path into the key
		/// </summary>
		public static T GetRegisteredTaskObjectAssemblyLocal<T> (this IBuildEngine4 engine, object key, RegisteredTaskObjectLifetime lifetime, RegisterTaskObjectKeyFlags flags = RegisterTaskObjectKeyFlags.IncludeProjectFile)
			where T : class =>
			engine.GetRegisteredTaskObject (engine.GetKey (AssemblyLocation, key, flags), lifetime) as T;

		/// <summary>
		/// IBuildEngine4.UnregisterTaskObject, but adds the current assembly path into the key
		/// </summary>
		[Obsolete ("Use UnregisterTaskObjectAssemblyLocal (engine, key, lifetime, flags) instead.")]
		public static object UnregisterTaskObjectAssemblyLocal (this IBuildEngine4 engine, object key, RegisteredTaskObjectLifetime lifetime) =>
			UnregisterTaskObjectAssemblyLocal (engine, key, lifetime, flags: RegisterTaskObjectKeyFlags.IncludeProjectFile);

		/// <summary>
		/// IBuildEngine4.UnregisterTaskObject, but adds the current assembly path into the key
		/// </summary>
		public static object UnregisterTaskObjectAssemblyLocal (this IBuildEngine4 engine, object key, RegisteredTaskObjectLifetime lifetime, RegisterTaskObjectKeyFlags flags = RegisterTaskObjectKeyFlags.IncludeProjectFile) =>
			engine.UnregisterTaskObject (engine.GetKey (AssemblyLocation, key, flags), lifetime);

		/// <summary>
		/// Generic version of IBuildEngine4.UnregisterTaskObject, but adds the current assembly path into the key
		/// </summary>
		[Obsolete ("Use UnregisterTaskObjectAssemblyLocal<T> (engine, key, lifetime, flags) instead.")]
		public static T UnregisterTaskObjectAssemblyLocal<T> (this IBuildEngine4 engine, object key, RegisteredTaskObjectLifetime lifetime)
			where T : class => UnregisterTaskObjectAssemblyLocal<T> (engine, key, lifetime, flags: RegisterTaskObjectKeyFlags.IncludeProjectFile);

		/// <summary>
		/// Generic version of IBuildEngine4.UnregisterTaskObject, but adds the current assembly path into the key
		/// </summary>
		public static T UnregisterTaskObjectAssemblyLocal<T> (this IBuildEngine4 engine, object key, RegisteredTaskObjectLifetime lifetime, RegisterTaskObjectKeyFlags flags = RegisterTaskObjectKeyFlags.IncludeProjectFile)
			where T : class =>
			engine.UnregisterTaskObject (engine.GetKey (AssemblyLocation, key, flags), lifetime) as T;

		/// <summary>
		/// Method to calculate the key for the RegisterTaskObject. This is based on the
		/// RegisterTaskObjectKeyFlags which are passed.
		/// </summary>
		static object GetKey (this IBuildEngine4 engine, string location, object key, RegisterTaskObjectKeyFlags flags)
		{
			return ((flags & RegisterTaskObjectKeyFlags.IncludeProjectFile) != 0)
				? (location, key, engine.ProjectFileOfTaskNode)
				: (location, key, string.Empty);
		}
	}
}
