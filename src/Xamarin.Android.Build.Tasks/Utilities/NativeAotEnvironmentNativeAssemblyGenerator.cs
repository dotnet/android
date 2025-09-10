using System;
using System.Collections.Generic;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Tasks;

class NativeAotEnvironmentNativeAssemblyGenerator : LlvmIrComposer
{
	readonly EnvironmentBuilder envBuilder;
	StructureInfo? appEnvironmentVariableStructureInfo;

	public NativeAotEnvironmentNativeAssemblyGenerator (TaskLoggingHelper log, EnvironmentBuilder envBuilder)
		: base (log)
	{
		this.envBuilder = envBuilder;
	}

	protected override void Construct (LlvmIrModule module)
	{
		MapStructures (module);

		SortedDictionary<string, string>? environmentVariables = null;
		if (envBuilder.EnvironmentVariables.Count > 0) {
			environmentVariables = new (envBuilder.EnvironmentVariables, StringComparer.Ordinal);
		}

		SortedDictionary<string, string>? systemProperties = null;
		if (envBuilder.SystemProperties.Count > 0) {
			systemProperties = new (envBuilder.SystemProperties, StringComparer.Ordinal);
		}

		var envVarsBlob = new LlvmIrStringBlob ();
		List<StructureInstance<LlvmIrHelpers.AppEnvironmentVariable>> appEnvVars = LlvmIrHelpers.MakeEnvironmentVariableList (
			Log,
			environmentVariables,
			envVarsBlob,
			appEnvironmentVariableStructureInfo
		);

		var envVarsCount = new LlvmIrGlobalVariable ((uint)appEnvVars.Count, "__naot_android_app_environment_variable_count");
		module.Add (envVarsCount);

		var envVars = new LlvmIrGlobalVariable (appEnvVars, "__naot_android_app_environment_variables") {
			Comment = " Application environment variables array, name:value",
			Options = LlvmIrVariableOptions.GlobalConstant,
		};
		module.Add (envVars);
		module.AddGlobalVariable ("__naot_android_app_environment_variable_contents", envVarsBlob, LlvmIrVariableOptions.GlobalConstant);
	}

	void MapStructures (LlvmIrModule module)
	{
		appEnvironmentVariableStructureInfo = module.MapStructure<LlvmIrHelpers.AppEnvironmentVariable> ();
	}
}
