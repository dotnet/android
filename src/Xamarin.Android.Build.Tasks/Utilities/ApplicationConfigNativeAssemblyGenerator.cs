using System;
using System.Collections.Generic;
using System.IO;

using Java.Interop.Tools.TypeNameMappings;

namespace Xamarin.Android.Tasks
{
	class ApplicationConfigNativeAssemblyGenerator : NativeAssemblyGenerator
	{
		SortedDictionary <string, string> environmentVariables;
		SortedDictionary <string, string> systemProperties;
		uint stringCounter = 0;

		public bool IsBundledApp { get; set; }
		public bool UsesMonoAOT { get; set; }
		public bool UsesMonoLLVM { get; set; }
		public bool UsesAssemblyPreload { get; set; }
		public string MonoAOTMode { get; set; }
		public string AndroidPackageName { get; set; }
		public bool BrokenExceptionTransitions { get; set; }
		public global::Android.Runtime.BoundExceptionType BoundExceptionType { get; set; }
		public bool InstantRunEnabled { get; set; }

		public PackageNamingPolicy PackageNamingPolicy { get; set; }

		public ApplicationConfigNativeAssemblyGenerator (NativeAssemblerTargetProvider targetProvider, string baseFileName, IDictionary<string, string> environmentVariables, IDictionary<string, string> systemProperties)
			: base (targetProvider, baseFileName)
		{
			if (environmentVariables != null)
				this.environmentVariables = new SortedDictionary<string, string> (environmentVariables, StringComparer.Ordinal);
			if (systemProperties != null)
				this.systemProperties = new SortedDictionary<string, string> (systemProperties, StringComparer.Ordinal);
		}

		protected override void WriteSymbols (StreamWriter output)
		{
			if (String.IsNullOrEmpty (AndroidPackageName))
				throw new InvalidOperationException ("Android package name must be set");

			if (UsesMonoAOT && String.IsNullOrEmpty (MonoAOTMode))
				throw new InvalidOperationException ("Mono AOT enabled but no AOT mode specified");

			string stringLabel = GetStringLabel ();
			WriteData (output, AndroidPackageName, stringLabel);

			WriteDataSection (output, "application_config");
			WriteSymbol (output, "application_config", TargetProvider.GetStructureAlignment (true), packed: false, isGlobal: true, alwaysWriteSize: true, structureWriter: () => {
				// Order of fields and their type must correspond *exactly* to that in
				// src/monodroid/jni/xamarin-app.h ApplicationConfig structure
				WriteCommentLine (output, "uses_mono_llvm");
				uint size = WriteData (output, UsesMonoLLVM);

				WriteCommentLine (output, "uses_mono_aot");
				size += WriteData (output, UsesMonoAOT);

				WriteCommentLine (output, "uses_assembly_preload");
				size += WriteData (output, UsesAssemblyPreload);

				WriteCommentLine (output, "is_a_bundled_app");
				size += WriteData (output, IsBundledApp);

				WriteCommentLine (output, "broken_exception_transitions");
				size += WriteData (output, BrokenExceptionTransitions);

				WriteCommentLine (output, "instant_run_enabled");
				size += WriteData (output, InstantRunEnabled);

				WriteCommentLine (output, "bound_exception_type");
				size += WriteData (output, (byte)BoundExceptionType);

				WriteCommentLine (output, "package_naming_policy");
				size += WriteData (output, (uint)PackageNamingPolicy);

				WriteCommentLine (output, "environment_variable_count");
				size += WriteData (output, environmentVariables == null ? 0 : environmentVariables.Count * 2);

				WriteCommentLine (output, "system_property_count");
				size += WriteData (output, systemProperties == null ? 0 : systemProperties.Count * 2);

				WriteCommentLine (output, "android_package_name");
				size += WritePointer (output, MakeLocalLabel (stringLabel));

				return size;
			});

			stringLabel = GetStringLabel ();
			WriteData (output, MonoAOTMode ?? String.Empty, stringLabel);
			WriteDataSection (output, "mono_aot_mode_name");
			WritePointer (output, MakeLocalLabel (stringLabel), "mono_aot_mode_name", isGlobal: true);

			WriteNameValueStringArray (output, "app_environment_variables", environmentVariables);
			WriteNameValueStringArray (output, "app_system_properties", systemProperties);
		}

		void WriteNameValueStringArray (StreamWriter output, string label, SortedDictionary<string, string> entries)
		{
			if (entries == null || entries.Count == 0) {
				WriteDataSection (output, label);
				WriteSymbol (output, label, TargetProvider.GetStructureAlignment (true), packed: false, isGlobal: true, alwaysWriteSize: true, structureWriter: null);
				return;
			}

			var entry_labels = new List <string> ();
			foreach (var kvp in entries) {
				string name = kvp.Key;
				string value = kvp.Value ?? String.Empty;
				string stringLabel = GetStringLabel ();
				WriteData (output, name, stringLabel);
				entry_labels.Add (stringLabel);

				stringLabel = GetStringLabel ();
				WriteData (output, value, stringLabel);
				entry_labels.Add (stringLabel);

			}

			WriteDataSection (output, label);
			WriteSymbol (output, label, TargetProvider.GetStructureAlignment (true), packed: false, isGlobal: true, alwaysWriteSize: true, structureWriter: () => {
				uint size = 0;

				foreach (string l in entry_labels) {
					size += WritePointer (output, MakeLocalLabel (l));
				}

				return size;
			});
		}

		void WriteDataSection (StreamWriter output, string tag)
		{
			WriteSection (output, $".data.{tag}", hasStrings: false, writable: true);
		}

		string GetStringLabel ()
		{
			stringCounter++;
			return $"env.str.{stringCounter}";
		}
	};
}
