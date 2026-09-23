#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class NativeAotEnvironmentNativeAssemblyGenerator
{
	readonly AppEnvironmentVariableTable environmentVariables;
	readonly AppEnvironmentVariableTable systemProperties;

	public NativeAotEnvironmentNativeAssemblyGenerator (TaskLoggingHelper log, EnvironmentBuilder envBuilder)
	{
		if (log == null) {
			throw new ArgumentNullException (nameof (log));
		}

		environmentVariables = new AppEnvironmentVariableTable (log, new SortedDictionary<string, string> (envBuilder.EnvironmentVariables, StringComparer.Ordinal));
		systemProperties = new AppEnvironmentVariableTable (log, new SortedDictionary<string, string> (envBuilder.SystemProperties, StringComparer.Ordinal));
	}

	public void Generate (AndroidTargetArch arch, TextWriter output, string fileName)
	{
		using var w = new LlvmIrWriter (output, LlvmIrTarget.Get (arch));
		w.WriteHeader (fileName);
		AppEnvironmentVariableTable.WriteDeclaration (w);

		w.WriteGlobal ("__naot_android_app_environment_variable_count", LlvmIrWriter.GlobalConstant, "i32", environmentVariables.Count.ToString (), 4);
		environmentVariables.Write (w, "__naot_android_app_environment_variables", "__naot_android_app_environment_variable_contents", " Application environment variables array, name:value");

		w.WriteGlobal ("__naot_android_app_system_property_count", LlvmIrWriter.GlobalConstant, "i32", systemProperties.Count.ToString (), 4);
		// We reuse the same structure as for environment variables, there's no point in adding a new, identical, one
		systemProperties.Write (w, "__naot_android_app_system_properties", "__naot_android_app_system_property_contents", " System properties defined by the application");

		w.WriteMetadata ();
		output.Flush ();
	}
}
