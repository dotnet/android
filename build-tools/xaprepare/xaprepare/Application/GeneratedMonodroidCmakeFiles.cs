using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Xamarin.Android.Prepare
{
	partial class GeneratedMonodroidCmakeFiles : GeneratedFile
	{
		string outputPath;

		public GeneratedMonodroidCmakeFiles (string outputPath)
			: base (MakeOutputPath (outputPath))
		{
			this.outputPath = outputPath;
		}

		static string MakeOutputPath (string outputPath)
		{
			return Path.Combine (outputPath, $"{{{Configurables.Paths.CmakeMSBuildPropsName},{Configurables.Paths.CmakeShellScriptsPropsName},{Configurables.Paths.CmakeMonodroidTargets}}}");
		}

		public override void Generate (Context context)
		{
			using (StreamWriter sw = Utilities.OpenStreamWriter (Path.Combine (outputPath, Configurables.Paths.CmakeMSBuildPropsName))) {
				GenerateMSBuildProps (context, sw);
				sw.Flush ();
			}

			using (StreamWriter sw = Utilities.OpenStreamWriter (Path.Combine (outputPath, Configurables.Paths.CmakeMonodroidTargets))) {
				GenerateMonodroidTargets (context, sw);
				sw.Flush ();
			}

			using (StreamWriter sw = Utilities.OpenStreamWriter (Path.Combine (outputPath, Configurables.Paths.CmakeShellScriptsPropsName))) {
				GenerateShellConfig (context, sw);
				sw.Flush ();
			}
		}

		static readonly Dictionary<string, string> ApiLevelVariableNames = new Dictionary<string, string> (StringComparer.Ordinal) {
			{ AbiNames.TargetJit.AndroidArmV7a, "NDK_LEGACY_API_ARMV7A" },
			{ BuildAndroidPlatforms.AndroidArmV7a_NET6, "NDK_NET6_API_ARMV7A" },
			{ AbiNames.TargetJit.AndroidArmV8a, "NDK_LEGACY_API_ARMV8A" },
			{ BuildAndroidPlatforms.AndroidArmV8a_NET6, "NDK_NET6_API_ARMV8A" },
			{ AbiNames.TargetJit.AndroidX86, "NDK_LEGACY_API_X86" },
			{ BuildAndroidPlatforms.AndroidX86_NET6, "NDK_NET6_API_X86" },
			{ AbiNames.TargetJit.AndroidX86_64, "NDK_LEGACY_API_X86_64" },
			{ BuildAndroidPlatforms.AndroidX86_64_NET6, "NDK_NET6_API_X86_64" },
		};

		static readonly Dictionary<string, string> JitAbis = new Dictionary<string, string> (StringComparer.Ordinal) {
			{ AbiNames.TargetJit.AndroidArmV7a, BuildAndroidPlatforms.AndroidArmV7a_NET6 },
			{ AbiNames.TargetJit.AndroidArmV8a, BuildAndroidPlatforms.AndroidArmV8a_NET6 },
			{ AbiNames.TargetJit.AndroidX86, BuildAndroidPlatforms.AndroidX86_NET6 },
			{ AbiNames.TargetJit.AndroidX86_64, BuildAndroidPlatforms.AndroidX86_64_NET6 },
		};

		static readonly Dictionary<string, string> HostAbis = new Dictionary<string, string> (StringComparer.Ordinal) {
			{ Context.Instance.OS.Type, String.Empty },
			{ AbiNames.HostJit.Win32, String.Empty },
			{ AbiNames.HostJit.Win64, String.Empty },
		};

		void GenerateShellConfig (Context context, StreamWriter sw)
		{
			var commonReplacements = new Dictionary<string, string> (StringComparer.Ordinal) {
				{ "@BUILD_TYPE@", "${__BUILD_TYPE}" },
				{ "@CONFIGURATION@", "${__CONFIGURATION}" },
				{ "@XA_BUILD_CONFIGURATION@", "${XA_BUILD_CONFIGURATION}" },
				{ "@JdkIncludePath@", "${JDK_INCLUDE_PATH}" },
				{ "@MonoSourceFullPath@", "${MONO_SOURCE_PATH}" },
				{ "@NinjaPath@", "${NINJA}" },
				{ "@OUTPUT_DIRECTORY@", "${__OUTPUT_DIR}" },
				{ "@SOURCE_DIRECTORY@", "${MONODROID_SOURCE_DIR}" },
			};

			var androidRuntimeReplacements = new Dictionary<string, string> (StringComparer.Ordinal) {
				{ "@CmakeAndroidFlags@", "" },
				{ "@NATIVE_API_LEVEL@", "${__NATIVE_API_LEVEL}" },
				{ "@ABI@", "${__NATIVE_ABI}" },
				{ "@AndroidNdkDirectory@", "${NDK_DIRECTORY}" },
				{ "@AndroidToolchainPath@", GetRelativeToolchainDefinitionPath () },
			};

			var hostRuntimeReplacements = new Dictionary<string, string> (StringComparer.Ordinal) {
				{ "@CmakeHostFlags@", "" },
				{ "@MingwDependenciesRootDirectory@", CmakeBuilds.MingwDependenciesRootDirectory },
				{ "@MxeToolchainBasePath@", "${MXE_TOOLCHAIN_BASE_PATH}" },
				{ "@BITNESS@", "" },
			};

			AddReplacements (commonReplacements, androidRuntimeReplacements);
			AddReplacements (commonReplacements, hostRuntimeReplacements);

			string monodroidObjDir = Path.Combine (Configurables.Paths.MonodroidSourceDir, "obj", context.Configuration);
			string jdkInfoPropsPath = Path.Combine (Configurables.Paths.ExternalJavaInteropDir, "bin", $"Build{context.Configuration}", "JdkInfo.props");

			sw.WriteLine ("# This is a bash(1) script");
			sw.WriteLine ();
			sw.WriteLine ($"CMAKE=\"{context.Properties.GetRequiredValue(KnownProperties.CMakePath)}\"");
			sw.WriteLine ($"JDK_INCLUDE_PATH=\"$(grep JdkIncludePath {jdkInfoPropsPath} | cut -d '\"' -f 2 | tr '\\n' ' ')\"");
			sw.WriteLine ($"MONO_SOURCE_PATH=\"{Configurables.Paths.MonoSourceFullPath}\"");
			sw.WriteLine ($"MONODROID_OBJ_DIR=\"{monodroidObjDir}\"");
			sw.WriteLine ($"MONODROID_SOURCE_DIR=\"{Configurables.Paths.MonodroidSourceDir}\"");
			sw.WriteLine ($"MXE_TOOLCHAIN_BASE_PATH=\"{CmakeBuilds.MxeToolchainBasePath}\"");
			sw.WriteLine ($"NDK_DIRECTORY=\"{Configurables.Paths.AndroidNdkDirectory}\"");
			sw.WriteLine ($"NINJA=\"{context.Properties.GetRequiredValue(KnownProperties.NinjaPath)}\"");
			sw.WriteLine ($"XA_BUILD_CONFIGURATION={context.Configuration}");
			sw.WriteLine ($"XA_INSTALL_DIR=\"{Configurables.Paths.InstallMSBuildDir}/lib\"");
			sw.WriteLine ();

			WriteApiLevelVariable (AbiNames.TargetJit.AndroidArmV7a);
			WriteApiLevelVariable (BuildAndroidPlatforms.AndroidArmV7a_NET6);
			WriteApiLevelVariable (AbiNames.TargetJit.AndroidArmV8a);
			WriteApiLevelVariable (BuildAndroidPlatforms.AndroidArmV8a_NET6);
			WriteApiLevelVariable (AbiNames.TargetJit.AndroidX86);
			WriteApiLevelVariable (BuildAndroidPlatforms.AndroidX86_NET6);
			WriteApiLevelVariable (AbiNames.TargetJit.AndroidX86_64);
			WriteApiLevelVariable (BuildAndroidPlatforms.AndroidX86_64_NET6);
			sw.WriteLine ();

			string indent = "\t";
			sw.WriteLine ("function __xa_build()");
			sw.WriteLine ("{");

			WriteVariableValidationCode ("__BUILD_DIR");

			sw.WriteLine ($"{indent}\"${{NINJA}}\" -C \"${{__BUILD_DIR}}\" \"$@\"");
			sw.WriteLine ("}");
			sw.WriteLine ();

			sw.WriteLine ("function __xa_configure_android_runtime()");
			sw.WriteLine ("{");

			WriteVariableValidationCode ("__BUILD_DIR");
			WriteVariableValidationCode ("__CONFIGURATION");
			WriteVariableValidationCode ("__BUILD_TYPE");
			WriteVariableValidationCode ("__NATIVE_API_LEVEL");
			WriteVariableValidationCode ("__NATIVE_ABI");
			WriteVariableValidationCode ("__OUTPUT_DIR");

			sw.WriteLine ($"{indent}cleanup_build_dir");
			sw.WriteLine ();

			indent = "\t\t";
			var flags = new StringBuilder ();
			AppendFlags (flags, CmakeBuilds.CommonFlags, indent);
			AppendFlags (flags, CmakeBuilds.MonodroidCommonDefines, indent);
			AppendFlags (flags, CmakeBuilds.AndroidFlags, indent);
			AppendFlags (flags, CmakeBuilds.ConfigureAndroidRuntimeCommandsCommonFlags, indent);
			indent = "\t";

			WriteCmakeCall (flags, androidRuntimeReplacements);

			sw.WriteLine ("}");
			sw.WriteLine ();

			sw.WriteLine ("function __xa_configure_host_runtime()");
			sw.WriteLine ("{");
			sw.WriteLine ($"{indent}local is_mxe=$1");
			sw.WriteLine ();

			WriteVariableValidationCode ("__BUILD_DIR");
			WriteVariableValidationCode ("__CONFIGURATION");
			WriteVariableValidationCode ("__BUILD_TYPE");
			WriteVariableValidationCode ("__OUTPUT_DIR");

			sw.WriteLine ($"{indent}cleanup_build_dir");
			sw.WriteLine ();

			sw.WriteLine ($"{indent}shift");
			sw.WriteLine ();
			sw.WriteLine ($"{indent}if [ \"${{is_mxe}}\" = \"yes\" ]; then");
			indent = "\t\t";

			// Windows cross builds
			flags.Clear ();
			indent = "\t\t\t";
			AppendFlags (flags, CmakeBuilds.CommonFlags, indent);
			AppendFlags (flags, CmakeBuilds.MonodroidCommonDefines, indent);
			AppendFlags (flags, CmakeBuilds.MonodroidMxeCommonFlags, indent);
			AppendFlags (flags, CmakeBuilds.ConfigureHostRuntimeCommandsCommonFlags, indent);
			indent = "\t\t";
			WriteCmakeCall (flags, hostRuntimeReplacements);

			// Host build
			sw.WriteLine ($"\telse");
			indent = "\t\t\t";
			flags.Clear ();
			AppendFlags (flags, CmakeBuilds.CommonFlags, indent);
			AppendFlags (flags, CmakeBuilds.MonodroidCommonDefines, indent);
			AppendFlags (flags, CmakeBuilds.ConfigureHostRuntimeCommandsCommonFlags, indent);
			indent = "\t\t";
			WriteCmakeCall (flags, hostRuntimeReplacements);

			indent = "\t";
			sw.WriteLine ($"{indent}fi");

			sw.WriteLine ("}");
			sw.WriteLine ();

			foreach (CmakeBuilds.RuntimeCommand rc in CmakeBuilds.AndroidRuntimeCommands) {
				WriteShellRuntimeCommand (sw, JitAbis, rc, androidRuntimeReplacements);
			}

			foreach (CmakeBuilds.RuntimeCommand rc in CmakeBuilds.HostRuntimeCommands) {
				WriteShellRuntimeCommand (sw, HostAbis, rc, hostRuntimeReplacements);
			}

			void WriteCmakeCall (StringBuilder args, Dictionary<string, string> replacements)
			{
				sw.WriteLine ($"{indent}\"${{CMAKE}}\" \\");
				sw.WriteLine ($"{indent}\t-B \"${{__BUILD_DIR}}\" \\");
				sw.Write (ApplyReplacements (args, replacements).ToString ());
				sw.WriteLine (" \"$@\"");
			}

			void WriteApiLevelVariable (string abi)
			{
				sw.WriteLine ($"{ApiLevelVariableNames[abi]}={BuildAndroidPlatforms.NdkMinimumAPI[abi]}");
			}

			void AppendFlags (StringBuilder sb, List<string> flags, string indent)
			{
				if (sb.Length > 0) {
					sb.Append ($" \\\n{indent}");
				} else {
					sb.Append (indent);
				}

				sb.Append (String.Join ($" \\\n{indent}", flags));
			}

			void WriteVariableValidationCode (string varName)
			{
				sw.WriteLine ($"{indent}if [ -z \"${{{varName}}}\" ]; then");
				sw.WriteLine ($"{indent}\tdie \"Variable '{varName}' is empty\"");
				sw.WriteLine ($"{indent}fi");
				sw.WriteLine ();
			}
		}

		void WriteShellRuntimeCommand (StreamWriter sw, Dictionary<string, string> abis, CmakeBuilds.RuntimeCommand command, Dictionary<string, string> replacements)
		{
			var funcName = new StringBuilder ();
			string indent = "\t";

			foreach (var kvp in abis) {
				string abi = kvp.Key;
				string apiLevelVarName = command.IsNet6 ? kvp.Value : kvp.Key;
				string outputDirName = command.IsNet6 ? $"{abi}-net6" : abi;
				string isMxe = "no";
				string mxeBitness = String.Empty;
				bool forMxe = false;

				if (command.IsHost) {
					if (String.Compare (abi, AbiNames.HostJit.Win32, StringComparison.OrdinalIgnoreCase) == 0) {
						mxeBitness = "32";
					} else if (String.Compare (abi, AbiNames.HostJit.Win64, StringComparison.OrdinalIgnoreCase) == 0) {
						mxeBitness = "64";
					}

					if (mxeBitness.Length > 0) {
						isMxe = "yes";
						forMxe = true;
					}
				}

				if (command.IsHost) {
					outputDirName = $"host-{outputDirName}";
					abi = $"host-{abi}";
				}

				funcName.Clear ();
				funcName.Append (abi.ToLowerInvariant ());
				funcName.Append ('_');
				funcName.Append (command.Suffix.ToLowerInvariant ());
				funcName.Replace ('-', '_');

				sw.WriteLine ();
				sw.WriteLine ($"function _configure_{funcName}()");
				sw.WriteLine ("{");
				sw.WriteLine ($"{indent}local build_directory=\"$1\"");
				sw.WriteLine ($"{indent}local rebuild=\"$2\"");
				sw.WriteLine ($"{indent}local force=\"$3\"");
				sw.WriteLine ();
				sw.WriteLine ($"{indent}if [ -z \"${{build_directory}}\" ]; then");
				sw.WriteLine ($"{indent}\tbuild_directory=\"${{MONODROID_OBJ_DIR}}\"");
				sw.WriteLine ($"{indent}fi");
				sw.WriteLine ($"{indent}__BUILD_DIR=\"${{build_directory}}/{abi}-{command.Suffix}\"");
				sw.WriteLine ($"{indent}__OUTPUT_DIR=\"${{XA_INSTALL_DIR}}/{outputDirName}\"");
				sw.WriteLine ($"{indent}__CONFIGURATION={command.Configuration}");
				sw.WriteLine ($"{indent}__BUILD_TYPE={command.BuildType}");
				sw.WriteLine ($"{indent}__REBUILD=\"${{rebuild}}\"");
				sw.WriteLine ($"{indent}__FORCE=\"${{force}}\"");
				if (!command.IsHost) {
					sw.WriteLine ($"{indent}__NATIVE_ABI={abi}");
					sw.WriteLine ($"{indent}__NATIVE_API_LEVEL=${{{ApiLevelVariableNames[apiLevelVarName]}}}");
				}
				sw.WriteLine ();
				if (!command.IsHost) {
					sw.Write ($"{indent}__xa_configure_android_runtime");
				} else {
					sw.Write ($"{indent}__xa_configure_host_runtime {isMxe}");
				}

				StringBuilder? flags = null;
				if (forMxe) {
					flags = new StringBuilder (String.Join (" ", CmakeBuilds.MonodroidMxeCommonFlagsBitness));
					replacements["@BITNESS@"] = mxeBitness;
				}

				if (command.ExtraOptions != null && command.ExtraOptions.Count > 0) {
					if (flags == null) {
						flags = new StringBuilder ();
					} else {
						flags.Append (" ");
					}
					flags.Append (String.Join (" ", command.ExtraOptions));
				}

				if (flags != null && flags.Length > 0) {
					sw.Write (" ");
					sw.Write (ApplyReplacements (flags, replacements));
				}

				sw.WriteLine ();
				sw.WriteLine ();
				sw.WriteLine ("}");

				indent = "\t";
				sw.WriteLine ();
				sw.WriteLine ($"function _build_{funcName}()");
				sw.WriteLine ("{");
				sw.WriteLine ($"{indent}local build_directory=\"$1\"");
				sw.WriteLine ();
				sw.WriteLine ($"{indent}shift");
				sw.WriteLine ($"{indent}if [ -z \"${{build_directory}}\" ]; then");
				sw.WriteLine ($"{indent}\tbuild_directory=\"${{MONODROID_OBJ_DIR}}\"");
				sw.WriteLine ($"{indent}fi");
				sw.WriteLine ($"{indent}__BUILD_DIR=\"${{build_directory}}/{abi}-{command.Suffix}\"");
				sw.WriteLine ();
				sw.WriteLine ($"{indent}__xa_build \"$@\"");
				sw.WriteLine ("}");
			};
		}

		void GenerateMonodroidTargets (Context context, StreamWriter sw)
		{
			string sourceDir = Utilities.GetRelativePath (Configurables.Paths.BuildBinDir, Configurables.Paths.MonodroidSourceDir);

			var commonReplacements = new Dictionary<string, string> (StringComparer.Ordinal) {
				{ "@BUILD_TYPE@", "" },
				{ "@CONFIGURATION@", "" },
				{ "@SOURCE_DIRECTORY@", $"$(MSBuildThisFileDirectory){sourceDir}" },
			};

			var androidRuntimeReplacements = new Dictionary<string, string> (StringComparer.Ordinal) {
				{ "@CmakeAndroidFlags@", "$(_CmakeAndroidFlags)" },
				{ "@NATIVE_API_LEVEL@", "" },
				{ "@ABI@", "%(AndroidSupportedTargetJitAbi.Identity)" },
				{ "@OUTPUT_DIRECTORY@", "" },
			};

			var hostRuntimeReplacements = new Dictionary<string, string> (StringComparer.Ordinal) {
				{ "@CmakeHostFlags@", "%(_HostRuntime.CmakeFlags)" },
				{ "@JdkIncludePath@", "@(JdkIncludePath->'%(Identity)', ' ')" },
				{ "@OUTPUT_DIRECTORY@", "" },
			};

			AddReplacements (commonReplacements, androidRuntimeReplacements);
			AddReplacements (commonReplacements, hostRuntimeReplacements);

			WriteMSBuildProjectStart (sw);
			sw.WriteLine ("  <Target Name=\"_PrepareConfigureRuntimeCommands\" DependsOnTargets=\"_GetBuildHostRuntimes\">");

			string indent = "    ";
			foreach (CmakeBuilds.RuntimeCommand rc in CmakeBuilds.AndroidRuntimeCommands) {
				WriteMSBuildConfigureAndroidRuntimeCommands (sw, indent, rc, androidRuntimeReplacements);
			}

			foreach (CmakeBuilds.RuntimeCommand rc in CmakeBuilds.HostRuntimeCommands) {
				WriteMSBuildConfigureHostRuntimeCommands (sw, indent, rc, hostRuntimeReplacements);
			};

			sw.WriteLine ("  </Target>");
			WriteMSBuildProjectEnd (sw);
		}

		void WriteMSBuildConfigureRuntimeCommands (StreamWriter sw, string indent, string workingDirectory, string outputDirectory, string itemName, List<string> commonFlags, CmakeBuilds.RuntimeCommand command, Dictionary<string, string> replacements, bool needsApiLevel)
		{
			replacements["@CONFIGURATION@"] = EnsureRequired ("Configuration", command.Configuration);
			replacements["@BUILD_TYPE@"] = EnsureRequired ("BuildType", command.BuildType);
			replacements["@NATIVE_API_LEVEL@"] = needsApiLevel ? EnsureRequired ("MSBuildApiLevel", command.MSBuildApiLevel) : String.Empty;
			replacements["@OUTPUT_DIRECTORY@"] = outputDirectory;

			var flags = new StringBuilder ();
			flags.Append (String.Join (" ", commonFlags));

			if (command.ExtraOptions != null && command.ExtraOptions.Count > 0) {
				flags.Append (" ");
				flags.Append (String.Join (" ", command.ExtraOptions));
			}

			sw.WriteLine ($"{indent}<MakeDir Directories=\"{workingDirectory}\" />");
			sw.WriteLine ($"{indent}<ItemGroup>");
			sw.WriteLine ($"{indent}  <_ConfigureRuntimeCommands Include=\"{itemName}\">");
			sw.WriteLine ($"{indent}    <Command>$(CmakePath)</Command>");
			WriteProperty (sw, $"{indent}    ", "Arguments", flags, replacements);
			sw.WriteLine ($"{indent}    <WorkingDirectory>{workingDirectory}</WorkingDirectory>");
			sw.WriteLine ($"{indent}  </_ConfigureRuntimeCommands>");
			sw.WriteLine ($"{indent}</ItemGroup>");
			sw.WriteLine ();

			string EnsureRequired (string name, string v)
			{
				if (v.Length > 0) {
					return v;
				}

				throw new InvalidOperationException ($"RuntimeCommand.{name} must not be an empty string");
			}
		}

		void WriteMSBuildConfigureAndroidRuntimeCommands (StreamWriter sw, string indent, CmakeBuilds.RuntimeCommand command, Dictionary<string, string> replacements)
		{
			const string LegacyOutputDirectory = "$(OutputPath)%(AndroidSupportedTargetJitAbi.Identity)";
			const string Net6OutputDirectory = LegacyOutputDirectory + "-net6";

			WriteMSBuildConfigureRuntimeCommands (
				sw,
				indent,
				$"$(IntermediateOutputPath)%(AndroidSupportedTargetJitAbi.Identity)-{command.Suffix}",
				command.IsNet6 ? Net6OutputDirectory : LegacyOutputDirectory,
				"@(AndroidSupportedTargetJitAbi)",
				CmakeBuilds.ConfigureAndroidRuntimeCommandsCommonFlags,
				command,
				replacements,
				true
			);
		}

		void WriteMSBuildConfigureHostRuntimeCommands (StreamWriter sw, string indent, CmakeBuilds.RuntimeCommand command, Dictionary<string, string> replacements)
		{
			WriteMSBuildConfigureRuntimeCommands (
				sw,
				indent,
				$"$(IntermediateOutputPath)%(_HostRuntime.OutputDirectory)-{command.Suffix}",
				"$(OutputPath)/%(_HostRuntime.OutputDirectory)",
				"@(_HostRuntime)",
				CmakeBuilds.ConfigureHostRuntimeCommandsCommonFlags,
				command,
				replacements,
				false
			);
		}

		void GenerateMSBuildProps (Context context, StreamWriter sw)
		{
			var MSBuildReplacements = new Dictionary<string, string> (StringComparer.Ordinal) {
				{ "@NinjaPath@", "$(NinjaPath)" },
				{ "@XA_BUILD_CONFIGURATION@", "$(Configuration)" },
				{ "@AndroidNdkDirectory@", "$(AndroidNdkDirectory)" },
				{ "@MonoSourceFullPath@", "$(MonoSourceFullPath)" },
				{ "@AndroidToolchainPath@", GetRelativeToolchainDefinitionPath () },
			};

			var MSBuildMingwReplacements = new Dictionary<string, string> (StringComparer.Ordinal) {
				{ "@MingwDependenciesRootDirectory@", CmakeBuilds.MingwDependenciesRootDirectory },
				{ "@MxeToolchainBasePath@", CmakeBuilds.MxeToolchainBasePath },
				{ "@BITNESS@", "" },
			};

			WriteMSBuildProjectStart (sw);

			var sharedByAll = new StringBuilder ("--debug-output ");
			sharedByAll.Append (String.Join (" ", CmakeBuilds.CommonFlags));
			sharedByAll.Append (" ");
			sharedByAll.Append (String.Join (" ", CmakeBuilds.MonodroidCommonDefines));

			var flags = new StringBuilder ();
			flags.Append (sharedByAll);
			flags.Append (" ");
			flags.Append (String.Join (" ", CmakeBuilds.AndroidFlags));

			string propertyIndent = "    ";
			sw.WriteLine ("  <PropertyGroup>");
			WriteProperty (sw, propertyIndent, "_CmakeAndroidFlags", flags, MSBuildReplacements);
			WriteProperty (sw, propertyIndent, "_CmakeCommonHostFlags", sharedByAll, MSBuildReplacements);
			sw.WriteLine ("  </PropertyGroup>");

			if (CmakeBuilds.MxeToolchainBasePath.Length > 0 && CmakeBuilds.MingwDependenciesRootDirectory.Length > 0) {
				AddReplacements (MSBuildReplacements, MSBuildMingwReplacements);

				sw.WriteLine ();
				sw.WriteLine ("  <PropertyGroup>");

				flags.Clear ();
				flags.Append (sharedByAll);
				flags.Append (" ");
				flags.Append (String.Join (" ", CmakeBuilds.MonodroidMxeCommonFlags));
				flags.Append (" ");
				flags.Append (String.Join (" ", CmakeBuilds.MonodroidMxeCommonFlagsBitness));

				MSBuildMingwReplacements["@BITNESS@"] = "32";
				WriteProperty (sw, propertyIndent, "_CmakeMxeCommonFlags32", flags, MSBuildMingwReplacements);

				MSBuildMingwReplacements["@BITNESS@"] = "64";
				WriteProperty (sw, propertyIndent, "_CmakeMxeCommonFlags64", flags, MSBuildMingwReplacements);

				sw.WriteLine ("  </PropertyGroup>");
			}

			WriteMSBuildProjectEnd (sw);
		}

		StringBuilder ApplyReplacements (StringBuilder value, Dictionary<string, string> replacements)
		{
			var text = new StringBuilder ();
			text.Append (value);

			foreach (var kvp in replacements) {
				string placeholder = kvp.Key;
				string replacement = kvp.Value;

				text.Replace (placeholder, replacement);
			}

			return text;
		}

		void WriteProperty (StreamWriter sw, string indent, string name, StringBuilder value, Dictionary<string, string> replacements)
		{
			var text = ApplyReplacements (value, replacements);
			sw.WriteLine ($"{indent}<{name}>{text}</{name}>");
		}

		void WriteMSBuildProjectStart (StreamWriter sw)
		{
			sw.WriteLine ("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
			sw.WriteLine ("<Project ToolsVersion=\"4.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">");
		}

		void WriteMSBuildProjectEnd (StreamWriter sw)
		{
			sw.WriteLine ("</Project>");
		}

		string GetRelativeToolchainDefinitionPath ()
		{
			return Path.DirectorySeparatorChar + Path.Combine ("build", "cmake", "android.toolchain.cmake");
		}

		void AddReplacements (Dictionary<string, string> source, Dictionary<string, string> target)
		{
			foreach (var kvp in source) {
				target.Add (kvp.Key, kvp.Value);
			}
		}
	}
}
