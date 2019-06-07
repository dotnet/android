using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	abstract partial class ToolRunner : AppObject
	{
		static readonly TimeSpan DefaultProcessTimeout = TimeSpan.FromMinutes (15);

		ThumbTwiddler twiddler;
		string programVersion;

		protected const ConsoleColor CommandMessageColor = ConsoleColor.White;

		protected abstract string DefaultToolExecutableName { get; }
		protected abstract string ToolName                  { get; }

		protected Context Context              { get; }
		public string FullToolPath             { get; }
		protected string VersionString         => GetVersion ();

		public bool EchoCmdAndArguments        { get; set; } = true;
		public bool EchoStandardError          { get; set; } = true;
		public bool EchoStandardOutput         { get; set; }
		public string LogMessageIndent         { get; set; } = String.Empty;
		public virtual TimeSpan ProcessTimeout { get; set; } = DefaultProcessTimeout;

		protected ToolRunner (Context context, Log log = null, string toolPath = null)
			: base (log)
		{
			Context = context ?? throw new ArgumentNullException (nameof (context));

			if (String.IsNullOrEmpty (toolPath)) {
				Log.DebugLine ($"Locating {ToolName} executable '{DefaultToolExecutableName}'");
				FullToolPath = context.OS.Which (DefaultToolExecutableName);
			} else {
				Log.DebugLine ($"Custom NuGet path: {toolPath}");
				if (toolPath.IndexOf (Path.DirectorySeparatorChar) < 0) {
					Log.DebugLine ($"Locating custom {ToolName} executable '{toolPath}'");
					FullToolPath = context.OS.Which (toolPath);
				} else if (Path.IsPathRooted (toolPath)) {
					Log.DebugLine ($"{ToolName} executable path is rooted, using verbatim");
					FullToolPath = toolPath;
				} else {
					Log.DebugLine ($"{ToolName} executable path is relative to Xamarin.Android repository root");
					FullToolPath = Path.Combine (BuildPaths.XamarinAndroidSourceRoot, toolPath);
				}
			}

			if (String.IsNullOrEmpty (FullToolPath))
				throw new InvalidOperationException ($"{ToolName} executable path must be specified");

			Log.DebugLine ($"Full {ToolName} executable path value: {FullToolPath}");
			if (!File.Exists (FullToolPath))
				throw new InvalidOperationException ($"{ToolName} executable '{FullToolPath}' not found");
		}

		protected virtual string GetLogMessage (ProcessRunner runner)
		{
			string message = $"{LogMessageIndent}{Path.GetFileName (FullToolPath)}";
			string formattedArguments = runner.Arguments;
			if (!String.IsNullOrEmpty (formattedArguments))
				message = $"{message} {runner.Arguments}";

			return $"{message} ";
		}

		protected void AddArguments (ProcessRunner runner, IEnumerable<string> arguments)
		{
			if (arguments == null)
				return;

			foreach (string a in arguments) {
				string arg = a?.Trim ();
				if (String.IsNullOrEmpty (arg))
					continue;

				runner.AddQuotedArgument (arg);
			}
		}

		protected virtual ProcessRunner CreateProcessRunner (params string[] initialParams)
		{
			string managedRunner = Context.OS.GetManagedProgramRunner (FullToolPath);
			if (managedRunner != null)
				managedRunner = Context.OS.Which (managedRunner);

			var runner = new ProcessRunner (managedRunner ?? FullToolPath, initialParams) {
				ProcessTimeout = ProcessTimeout,
				EchoCmdAndArguments = EchoCmdAndArguments,
				EchoStandardError = EchoStandardError,
				EchoStandardOutput = EchoStandardOutput,
			};

			if (Context.Instance.OS.EnvironmentVariables != null) {
				foreach (var kvp in Context.Instance.OS.EnvironmentVariables) {
					runner.Environment [kvp.Key] = kvp.Value;
				}
			}

			if (managedRunner != null)
				runner.AddQuotedArgument (FullToolPath);

			return runner;
		}

		protected virtual async Task<bool> RunTool (Func<bool> runner)
		{
			return await Task.Run (runner);
		}

		protected TextWriter SetupOutputSink (ProcessRunner runner, string tags = null, string messagePrefix = null)
		{
			string logFilePath = null;

			if (!String.IsNullOrEmpty (tags)) {
				logFilePath = Context.GetLogFilePath (tags ?? String.Empty);
				if (String.IsNullOrEmpty (messagePrefix))
					messagePrefix = "running";
				Log.StatusLine ($"{LogMessageIndent}[{ToolName}] {messagePrefix}");
				Log.StatusLine ($"[{ToolName}] log file: ", $"{Utilities.GetRelativePath (BuildPaths.XamarinAndroidSourceRoot, logFilePath)}", tailColor: Log.DestinationColor);
			}

			TextWriter ret = CreateLogSink (logFilePath);

			runner.AddStandardErrorSink (ret);
			runner.AddStandardOutputSink (ret);

			return ret;
		}

		protected virtual TextWriter CreateLogSink (string logFilePath)
		{
			throw new NotSupportedException ("Child class must implement this method if it uses GetLogFileSink");
		}

		protected void StartTwiddler (bool showElapsedTime = true)
		{
			twiddler = new ThumbTwiddler (Context.Instance, Context.Instance.DullMode, showElapsedTime);
			twiddler.Start ();
		}

		protected void StopTwiddler ()
		{
			if (twiddler == null)
				return;

			twiddler.Stop ();
			Log.StatusLine ();
		}

		string GetVersion ()
		{
			if (!String.IsNullOrEmpty (programVersion))
				return programVersion;

			(bool success, string version) = Utilities.GetProgramVersion (FullToolPath);
			if (!success)
				return null;

			programVersion = version;
			return version;
		}
	}
}
