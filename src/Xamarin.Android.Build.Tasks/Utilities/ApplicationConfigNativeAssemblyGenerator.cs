using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Tasks
{
	class ApplicationConfigNativeAssemblyGenerator : NativeAssemblyGenerator
	{
		SortedDictionary <string, string> environmentVariables;
		SortedDictionary <string, string> systemProperties;
		uint stringCounter = 0;

		public bool IsBundledApp { get; set; }
		public bool UsesEmbeddedDSOs { get; set; }
		public bool UsesMonoAOT { get; set; }
		public bool UsesMonoLLVM { get; set; }
		public bool UsesAssemblyPreload { get; set; }
		public string MonoAOTMode { get; set; }
		public string AndroidPackageName { get; set; }

		public ApplicationConfigNativeAssemblyGenerator (NativeAssemblerTargetProvider targetProvider, IDictionary<string, string> environmentVariables, IDictionary<string, string> systemProperties)
			: base (targetProvider)
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
			WriteSymbol (output, "application_config", TargetProvider.GetStructureAlignment (true), fieldAlignBytes: 4, isGlobal: true, alwaysWriteSize: true, structureWriter: () => {
				// Order of fields and their type must correspond *exactly* to that in
				// src/monodroid/jni/xamarin-app.h ApplicationConfig structure
				WriteCommentLine (output, "uses_mono_llvm");
				uint size = WriteData (output, UsesMonoLLVM);

				WriteCommentLine (output, "uses_mono_aot");
				size += WriteData (output, UsesMonoAOT);

				WriteCommentLine (output, "uses_embedded_dsos");
				size += WriteData (output, UsesEmbeddedDSOs);

				WriteCommentLine (output, "uses_assembly_preload");
				size += WriteData (output, UsesAssemblyPreload);

				WriteCommentLine (output, "is_a_bundled_app");
				size += WriteData (output, IsBundledApp);

				WriteCommentLine (output, "environment_variable_count");
				size += WriteData (output, environmentVariables == null ? 0 : environmentVariables.Count * 2);

				WriteCommentLine (output, "system_property_count");
				size += WriteData (output, systemProperties == null ? 0 : systemProperties.Count * 2);

				WriteCommentLine (output, "android_package_name");
				size += WritePointer (output, stringLabel);

				return size;
			});

			stringLabel = GetStringLabel ();
			WriteData (output, MonoAOTMode ?? String.Empty, stringLabel);
			WriteDataSection (output, "mono_aot_mode_name");
			WritePointer (output, stringLabel, "mono_aot_mode_name", isGlobal: true);

			WriteNameValueStringArray (output, "app_environment_variables", environmentVariables);
			WriteNameValueStringArray (output, "app_system_properties", systemProperties);
		}

		void WriteNameValueStringArray (StreamWriter output, string label, SortedDictionary<string, string> entries)
		{
			if (entries == null || entries.Count == 0) {
				WriteDataSection (output, label);
				WriteSymbol (output, label, TargetProvider.GetStructureAlignment (true), fieldAlignBytes: 4, isGlobal: true, alwaysWriteSize: true, structureWriter: null);
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
			WriteSymbol (output, label, TargetProvider.GetStructureAlignment (true), fieldAlignBytes: 4, isGlobal: true, alwaysWriteSize: true, structureWriter: () => {
				uint size = 0;

				foreach (string l in entry_labels) {
					size += WritePointer (output, l);
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
			return $".L.str.{stringCounter}";
		}
	};
}
