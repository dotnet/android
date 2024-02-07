using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Mono.Options;

namespace Xamarin.Android.Prepare
{
	class App
	{
		sealed class ParsedOptions
		{
			public bool ShowHelp               { get; set; } = false;
			public bool DumpProps              { get; set; } = false;
			public bool NoEmoji                { get; set; } = !Configurables.Defaults.UseEmoji;
			public string? HashAlgorithm       { get; set; }
			public uint MakeConcurrency        { get; set; } = 0;
			public ExecutionMode ExecutionMode { get; set; } = Configurables.Defaults.ExecutionMode;
			public LoggingVerbosity Verbosity  { get; set; } = Configurables.Defaults.LoggingVerbosity;
			public string DebugFileExtension   { get; set; } = Configurables.Defaults.DebugFileExtension;
			public string? ScenarioName        { get; set; }
			public bool ListScenarios          { get; set; } = false;
			public string CompressionFormat    { get; set; } = Configurables.Defaults.DefaultCompressionFormat.Name;
			public string? Configuration       { get; set; }
			public bool AutoProvision          { get; set; }
			public bool AutoProvisionUsesSudo  { get; set; }
			public bool IgnoreMaxMonoVersion   { get; set; }
			public bool IgnoreMinMonoVersion   { get; set; }
			public RefreshableComponent RefreshList { get; set; }
			public IEnumerable<string> AndroidSdkPlatforms { get; set; } = new [] { "latest" };
		}

		public static int Main (string[] args)
		{
			try {
				return Run (args).Result;
			} catch (AggregateException aex) {
				foreach (Exception ex in aex.InnerExceptions) {
					PrintException (ex);
				}
			} catch (Exception ex) {
				PrintException (ex);
			} finally {
				Log.Instance.Dispose ();
				ResetConsoleColors ();
			}

			return 1;

			void PrintException (Exception ex)
			{
				Log.Instance.ErrorLine (showSeverity: false);
				Log.Instance.ErrorLine (ex.Message, showSeverity: false);
				Log.Instance.ErrorLine (ex.ToString (), showSeverity: false);
				Log.Instance.ErrorLine (showSeverity: false);
			}
		}

		static void ResetConsoleColors ()
		{
			try {
				Console.CursorVisible = true;
				Console.ResetColor ();
			} catch {
				// Ignore
			}
		}

		static async Task<int> Run (string[] args)
		{
			Log.SetContext (Context.Instance);

			// Kajabity requires an encoding (iso-8859-4) that does not ship with .NET Core by default.
			Encoding.RegisterProvider (CodePagesEncodingProvider.Instance);

			var optionErrors = new List <string> ();
			ParsedOptions parsedOptions = new ParsedOptions {
				AutoProvision         = ParseBoolean (Context.Instance.Properties.GetValue (KnownProperties.AutoProvision)),
				AutoProvisionUsesSudo = ParseBoolean (Context.Instance.Properties.GetValue (KnownProperties.AutoProvisionUsesSudo)),
				IgnoreMaxMonoVersion  = ParseBoolean (Context.Instance.Properties.GetValue (KnownProperties.IgnoreMaxMonoVersion)),
			};

			var opts = new OptionSet {
				"Usage: xaprepare [OPTIONS]",
				$"Xamarin.Android v{BuildInfo.XAVersion} preparation utility",
				"",
				{"p|property={=}", "Set a {PROPERTY} to a {VALUE}", (string p, string v) => Context.Instance.Properties.Set (p, v) },
				{"d|dump-properties", "Dump values of all the defined properties to the screen", v => parsedOptions.DumpProps = true },
				{"j|make-concurrency=", "Number of concurrent jobs for make to run. A positive integer or 0 for the default. Defaults to the number of CPUs/cores", v => parsedOptions.MakeConcurrency = EnsureUInt (v, "Invalid Make concurrency value") },
				{"no-emoji", "Do not use any emoji characters in the output", v => parsedOptions.NoEmoji = true },
				{"r|run-mode=", $"Specify the execution mode: {GetExecutionModes()}. See documentation for mode descriptions. Default: {Configurables.Defaults.ExecutionMode}", v => parsedOptions.ExecutionMode = ParseExecutionMode (v)},
				{"H|hash-algorithm=", "Use the specified hash algorithm instead of the default {Configurables.Defaults.HashAlgorithm}", v => parsedOptions.HashAlgorithm = v?.Trim () },
				{"D|debug-ext=", $"Extension of files with debug information for managed DLLs and executables. Default: {parsedOptions.DebugFileExtension}", v => parsedOptions.DebugFileExtension = v?.Trim () ?? String.Empty },
				{"v|verbosity=", $"Set console log verbosity to {{LEVEL}}. Level name may be abbreviated to the smallest unique part (one of: {GetVerbosityLevels ()}). Default: {Context.Instance.LoggingVerbosity.ToString().ToLowerInvariant ()}", v => parsedOptions.Verbosity = ParseLogVerbosity (v) },
				{"s|scenario=", "Run the specified scenario (use --ls to list all known scenarios) instead of the default one", v => parsedOptions.ScenarioName = v },
				{"ls", "List names of all known scenarios", v => parsedOptions.ListScenarios = true },
				{"cf=", $"{{NAME}} of the compression format to use for some archives (e.g. the XA bundle). One of: {GetCompressionFormatNames ()}; Default: {parsedOptions.CompressionFormat}", v => parsedOptions.CompressionFormat = v?.Trim () ?? String.Empty},
				{"c|configuration=", $"Build {{CONFIGURATION}}. Default: {Context.Instance.Configuration}", v => parsedOptions.Configuration = v?.Trim ()},
				{"refresh:", "[sdk,ndk] Comma separated list of components which should be reinstalled. Defaults to all supported components if no value is provided.", v => parsedOptions.RefreshList = ParseRefreshableComponents (v?.Trim () ?? String.Empty)},
				"",
				{"auto-provision=", $"Automatically install software required by Xamarin.Android", v => parsedOptions.AutoProvision = ParseBoolean (v)},
				{"auto-provision-uses-sudo=", $"Allow use of sudo(1) when provisioning", v => parsedOptions.AutoProvisionUsesSudo = ParseBoolean (v)},
				{"ignore-max-mono-version=", $"Ignore the maximum supported Mono version restriction", v => parsedOptions.IgnoreMaxMonoVersion = ParseBoolean (v)},
				{"ignore-min-mono-version=", $"Ignore the minimum supported Mono version restriction", v => parsedOptions.IgnoreMinMonoVersion = ParseBoolean (v)},
				{"android-sdk-platforms=", "Comma separated list of Android SDK platform levels to be installed or 'latest' or 'all'. Defaults to 'latest' if no value is provided.", v => parsedOptions.AndroidSdkPlatforms = ParseAndroidSdkPlatformLevels (v?.Trim () ?? String.Empty) },
				"",
				{"h|help", "Show this help message", v => parsedOptions.ShowHelp = true },
			};

			opts.Parse (args);
			if (parsedOptions.ShowHelp) {
				opts.WriteOptionDescriptions (Console.Out);
				return 0;
			}

			if (optionErrors.Count > 0) {
				Log.Instance.ErrorLine ("Invalid arguments passed, please run with --help to see option documentation:");
				Log.Instance.ErrorLine ();
				foreach (string errorLine in optionErrors) {
					Log.Instance.ErrorLine ($"  {errorLine}");
				}
			}

			// If we're running without a terminal or the output is redirected we must enforce the dull mode.
			if (!Context.Instance.InteractiveSession)
				parsedOptions.ExecutionMode = ExecutionMode.CI;

			Context.Instance.MakeConcurrency       = parsedOptions.MakeConcurrency;
			Context.Instance.NoEmoji               = parsedOptions.NoEmoji;
			Context.Instance.ExecutionMode         = parsedOptions.ExecutionMode;
			Context.Instance.LoggingVerbosity      = parsedOptions.Verbosity;
			Context.Instance.DebugFileExtension    = parsedOptions.DebugFileExtension;
			Context.Instance.AutoProvision         = parsedOptions.AutoProvision;
			Context.Instance.AutoProvisionUsesSudo = parsedOptions.AutoProvisionUsesSudo;
			Context.Instance.IgnoreMaxMonoVersion  = parsedOptions.IgnoreMaxMonoVersion;
			Context.Instance.IgnoreMinMonoVersion  = parsedOptions.IgnoreMinMonoVersion;
			Context.Instance.ComponentsToRefresh   = parsedOptions.RefreshList;
			Context.Instance.AndroidSdkPlatforms   = parsedOptions.AndroidSdkPlatforms;

			if (!String.IsNullOrEmpty (parsedOptions.Configuration))
				Context.Instance.Configuration = parsedOptions.Configuration!;

			if (!String.IsNullOrEmpty (parsedOptions.HashAlgorithm))
				Context.Instance.HashAlgorithm = parsedOptions.HashAlgorithm!;

			SetCompressionFormat (parsedOptions.CompressionFormat);

			if (!await Context.Instance.Init (parsedOptions.ScenarioName)) {
				return 1;
			}

			if (parsedOptions.DumpProps)
				DumpProperties (Context.Instance);
			if (parsedOptions.ListScenarios) {
				ListScenarios (Context.Instance);
				return 0;
			}

			return await Context.Instance.Execute () ? 0 : 1;

			uint EnsureUInt (string v, string errorText)
			{
				if (UInt32.TryParse (v, out uint ret))
					return ret;

				optionErrors.Add (errorText);
				return 0;
			}
		}

		static bool SetCompressionFormat (string cfName)
		{
			if (String.IsNullOrEmpty (cfName)) {
				Log.Instance.ErrorLine ("Compression format name must be specified");
				return false;
			}

			if (String.Compare (cfName, Configurables.Defaults.DefaultCompressionFormat.Name, StringComparison.OrdinalIgnoreCase) == 0)
				return true;

			if (!Configurables.Defaults.CompressionFormats.TryGetValue (cfName, out CompressionFormat? cf)) {
				Log.Instance.ErrorLine ($"Unknown compression format name: {cfName}");
				return false;
			}

			if (cf == null)
				throw new InvalidOperationException ($"Valid compression format name ({cfName}) but compression format is null!");

			Log.Instance.DebugLine ($"Setting compression format to: {cf.Description}; File extension: {cf.Extension}");
			Context.Instance.CompressionFormat = cf;

			return true;
		}

		static string GetCompressionFormatNames ()
		{
			return String.Join (", ", Configurables.Defaults.CompressionFormats.Keys);
		}

		static void ListScenarios (Context context)
		{
			Log.Instance.StatusLine ("Known scenarios:");
			foreach (var kvp in context.Scenarios) {
				Scenario scenario = kvp.Value;
				if (scenario == null)
					continue;

				Log.Instance.Status ($"  {context.Characters.Bullet} {scenario.Name}");
				Log.Instance.StatusLine (scenario == context.DefaultScenario ? " (default)" : String.Empty, ConsoleColor.Green);
			}
		}

		static void DumpProperties (Context context)
		{
			if (context.Properties.Count == 0) {
				Log.Instance.InfoLine ("No properties defined");
				return;
			}

			context.Banner ("Defined properties");
			foreach (var kvp in context.Properties) {
				Log.Instance.InfoLine ($"{kvp.Key} = ", kvp.Value ?? "<null>", ConsoleColor.White, ConsoleColor.White);
			}
		}

		static string GetVerbosityLevels ()
		{
			return EnumNamesToCommaSeparatedList<LoggingVerbosity> ();
		}

		static string GetExecutionModes ()
		{
			return EnumNamesToCommaSeparatedList<ExecutionMode> ();
		}

		static string EnumNamesToCommaSeparatedList<T> ()
		{
			return String.Join (", ", GetEnumNames<T> ());
		}

		static IEnumerable<string> GetEnumNames <T> ()
		{
			return Enum.GetNames (typeof (T)).Select (v => v.ToLowerInvariant ());
		}

		static LoggingVerbosity ParseLogVerbosity (string name)
		{
			switch (Char.ToLowerInvariant (name [0])) {
				case 's':
					return LoggingVerbosity.Silent;

				case 'q':
					return LoggingVerbosity.Quiet;

				case 'n':
					return LoggingVerbosity.Normal;

				case 'v':
					return LoggingVerbosity.Verbose;

				case 'd':
					return LoggingVerbosity.Diagnostic;

				default:
					throw new InvalidOperationException ($"Unknown logging verbosity level '{name}'");
			}
		}

		static ExecutionMode ParseExecutionMode (string name)
		{
			switch (Char.ToLowerInvariant (name [0])) {
				case 'c':
					return ExecutionMode.CI;

				case 's':
					return ExecutionMode.Standard;

				case 'i':
					return ExecutionMode.Interactive;

				default:
					throw new InvalidOperationException ($"Unknown execution mode '{name}'");
			}
		}

		static bool ParseBoolean (string? value)
		{
			string? v = value?.Trim ();
			if (String.IsNullOrEmpty (v))
				throw new ArgumentException ("must not be null or empty", nameof (v));

			if (String.Compare ("yes", v, StringComparison.OrdinalIgnoreCase) == 0 || String.Compare ("true", v, StringComparison.OrdinalIgnoreCase) == 0)
				return true;

			if (String.Compare ("no", v, StringComparison.OrdinalIgnoreCase) == 0 || String.Compare ("false", v, StringComparison.OrdinalIgnoreCase) == 0)
				return false;

			throw new InvalidOperationException ($"Unknown boolean value: {value}");
		}

		static RefreshableComponent ParseRefreshableComponents (string refreshList)
		{
			if (String.IsNullOrEmpty (refreshList))
				return RefreshableComponent.All;

			if (refreshList.IndexOf (',') == -1)
				return ParseSingleComponent (refreshList);

			var allParsedComponents = RefreshableComponent.None;
			var refreshListArray = refreshList.Split (',');
			foreach (var c in refreshListArray) {
				RefreshableComponent parsed = ParseSingleComponent (c);
				if (parsed != RefreshableComponent.None)
					allParsedComponents |= parsed;
			}

			return allParsedComponents;


			RefreshableComponent ParseSingleComponent (string component) {
				if (String.Compare ("sdk", component, StringComparison.OrdinalIgnoreCase) == 0)
					return RefreshableComponent.AndroidSDK;

				if (String.Compare ("ndk", component, StringComparison.OrdinalIgnoreCase) == 0)
					return RefreshableComponent.AndroidNDK;

				return RefreshableComponent.None;
			}
		}

		static IEnumerable<string> ParseAndroidSdkPlatformLevels (string list)
		{
			// If the user specified "all" we return 'all' to indicate that all platforms should be installed.
			if (string.Compare ("all", list, StringComparison.OrdinalIgnoreCase) == 0)
				return new string [] { "all" };

			// If the user did not specify anything, we return "latest" to indicate that only the latest platform should be installed.
			if (string.IsNullOrEmpty (list) || string.Compare ("latest", list, StringComparison.OrdinalIgnoreCase) == 0)
				return new string [] { "latest" };

			// The user specified a list of platform levels to install, so we should respect that.
			return list.Split (',').Select (item => item.Trim ());
		}
	}
}
