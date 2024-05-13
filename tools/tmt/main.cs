using System;
using System.Collections.Generic;

using Mono.Options;

namespace tmt
{
	sealed class ParsedOptions
	{
		bool onlyJava;
		bool onlyManaged;
		AndroidArch archFilter = AndroidArch.All;

		public bool ShowHelp             { get; set; }
		public bool Verbose              { get; set; }
		public bool ShortReport          { get; set; }
		public bool? LoadOnlyFirst       { get; set; } = null;
		public bool? GenerateReportFiles { get; set; } = null;
		public string OutputDirectory    { get; set; } = String.Empty;

		public AndroidArch ArchFilter {
			get => archFilter;
			set => archFilter = value;
		}

		public bool OnlyJava {
			get => onlyJava;
			set {
				onlyJava = value;
				if (value) {
					onlyManaged = false;
				}
			}
		}

		public bool OnlyManaged {
			get => onlyManaged;
			set {
				onlyManaged = value;
				if (value) {
					onlyJava = false;
				}
			}
		}

		public void SetArchitectures (string architectures)
		{
			if (architectures.Length == 0) {
				return;
			}

			AndroidArch parsedFilter = AndroidArch.None;
			string[] values = architectures.Split (new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string arch in values) {
				if (String.Compare ("ARM", arch, StringComparison.OrdinalIgnoreCase) == 0) {
					parsedFilter |= AndroidArch.ARM;
					continue;
				}

				if (String.Compare ("ARM64", arch, StringComparison.OrdinalIgnoreCase) == 0) {
					parsedFilter |= AndroidArch.ARM64;
					continue;
				}

				if (String.Compare ("X86", arch, StringComparison.OrdinalIgnoreCase) == 0) {
					parsedFilter |= AndroidArch.X86;
					continue;
				}

				if (String.Compare ("X86_64", arch, StringComparison.OrdinalIgnoreCase) == 0) {
					parsedFilter |= AndroidArch.X86_64;
					continue;
				}

				Log.Warning ($"Unknown architecture '{arch}'");
			}

			if (parsedFilter != AndroidArch.None) {
				archFilter = parsedFilter;
			}
		}
	}

	class TypeMapTool
	{
		public static int Main (string[] args)
		{
			var parsedOptions = new ParsedOptions ();

			var opts = new OptionSet {
				"Usage: tmt [OPTIONS] <FILE.apk|FILE.aab|libxamarin-app.so|PROJECT_DIR> [FILTER_REGEX]",
				"",
				"The only required parameter is a path to a location containing the type maps.",
				"It is a path to an APK archive, AAB archive, .NET for Android application",
				"shared library or a .NET for Android project directory with compiled application.",
				"",
				"The optional FILTER_REGEX argument following the path is a regular expression to",
				"apply to the mapped types in order to filter the results. Full output files are still created,",
				"however the matching entries are also printed to console.",
				"",
				"OPTIONS are:",
				"",
				{ "j|only-java", "Process only the java-to-managed map", v => parsedOptions.OnlyJava = true },
				{ "m|only-managed", "Process only the managed-to-java map", v => parsedOptions.OnlyManaged = true },
				{ "s|short-report", "Omit some map details from the report (e.g. MVID and TokenID from managed-to-java map)", v => parsedOptions.ShortReport = true },
				{ "o|output-directory=", "Write the report files in the {DIR} directory instead of the current one", v => parsedOptions.OutputDirectory = v?.Trim () ?? String.Empty },
				{ "a|arch=", "Limit reporting only to the specified architectures. {ARCH_LIST} is a comma-separated list of architectures (one of, case-insensitive: ARM, ARM64, X86, X86_64)", v => parsedOptions.SetArchitectures (v?.Trim () ?? String.Empty) },
				{ "1|only-first", "Process only the first shared library from the APK/AAB archive or a project directory. Architecture filter is ignored in this case. This is the default action when regex filtering is used, otherwise it defaults to `false`", v => parsedOptions.LoadOnlyFirst = true },
				{ "g|generate-files", "Generate report files. If regex filtering is used, this setting defaults to `false`, it is `true` otherwise.", v => parsedOptions.GenerateReportFiles = true },
				"",
				{ "v|verbose", "Show debug messages", v => parsedOptions.Verbose = true },
				{ "h|help|?", "Show this help screen", v => parsedOptions.ShowHelp = true },
			};

			List<string> rest = opts.Parse (args);
			Log.SetVerbose (parsedOptions.Verbose);
			if (rest.Count == 0 || parsedOptions.ShowHelp) {
				opts.WriteOptionDescriptions (Console.Out);
				return 0;
			}

			string loadFrom = rest[0];
			string filterRegex = String.Empty;

			if (rest.Count > 1) {
				filterRegex = rest [1].Trim ();
			}

			if (filterRegex.Length > 0) {
				parsedOptions.ArchFilter = AndroidArch.All;

				if (!parsedOptions.LoadOnlyFirst.HasValue) {
					parsedOptions.LoadOnlyFirst = true;
				}

				if (!parsedOptions.GenerateReportFiles.HasValue) {
					parsedOptions.GenerateReportFiles = false;
				}
			} else {
				if (!parsedOptions.LoadOnlyFirst.HasValue) {
					parsedOptions.LoadOnlyFirst = false;
				}

				if (!parsedOptions.GenerateReportFiles.HasValue) {
					parsedOptions.GenerateReportFiles = true;
				}
			}

			var report = new Report (parsedOptions.OutputDirectory, filterRegex, !parsedOptions.ShortReport, parsedOptions.OnlyJava, parsedOptions.OnlyManaged, parsedOptions.GenerateReportFiles.Value);
			var loader = new Loader (parsedOptions.ArchFilter, parsedOptions.LoadOnlyFirst.Value);
			List<ITypemap> typemaps = loader.TryLoad (loadFrom);
			if (typemaps.Count == 0) {
				Log.Error ($"No supported type maps found in '{loadFrom}");
				return 1;
			}

			bool somethingFailed = false;
			foreach (ITypemap typemap in typemaps) {
				Log.Info ($"{typemap.FullPath}:");

				if (!typemap.Load (parsedOptions.OutputDirectory, parsedOptions.GenerateReportFiles.Value)) {
					Log.Error ($"  load failed");
					continue;
				}

				Log.Info ($"  File Type: {typemap.Description}");
				Log.Info ($"  Format version: {typemap.FormatVersion}");
				Log.Info ($"  Map kind: {typemap.Map.Kind}");
				Log.Info ($"  Map architecture: {typemap.Map.Architecture}");
				Log.Info ($"  Managed to Java entries: {typemap.Map.ManagedToJava.Count}");
				Log.Info ($"  Java to Managed entries: {typemap.Map.JavaToManaged.Count} (without duplicates)");

				report.Generate (typemap);
				Log.Info ();
			}

			return somethingFailed ? 1 : 0;
		}
	}
}
