using System;
using System.Collections.Generic;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Tasks;

class LlvmIrHelpers
{
	sealed class AppEnvironmentVariableContextDataProvider : NativeAssemblerStructContextDataProvider
	{
		public override string GetComment (object data, string fieldName)
		{
			var envVar = EnsureType<AppEnvironmentVariable> (data);

			if (MonoAndroidHelper.StringEquals ("name_index", fieldName)) {
				return $" '{envVar.Name}'";
			}

			if (MonoAndroidHelper.StringEquals ("value_index", fieldName)) {
				return $" '{envVar.Value}'";
			}

			return String.Empty;
		}
	}

	// Order of fields and their type must correspond *exactly* to that in
	// src/native/clr/include/xamarin-app.hh AppEnvironmentVariable structure
	[NativeAssemblerStructContextDataProvider (typeof (AppEnvironmentVariableContextDataProvider))]
	internal sealed class AppEnvironmentVariable
	{
		[NativeAssembler (Ignore = true)]
		public string? Name;

		[NativeAssembler (Ignore = true)]
		public string? Value;

		[NativeAssembler (UsesDataProvider = true)]
		public uint name_index;

		[NativeAssembler (UsesDataProvider = true)]
		public uint value_index;
	}

	public static void DeclareDummyFunction (LlvmIrModule module, LlvmIrGlobalVariableReference symref)
	{
		if (symref.Name.IsNullOrEmpty ()) {
			throw new InvalidOperationException ("Internal error: variable reference must have a name");
		}

		// Just a dummy declaration, we don't care about the arguments
		var funcSig = new LlvmIrFunctionSignature (symref.Name!, returnType: typeof(void));
		var _ = module.DeclareExternalFunction (funcSig);
	}

	public static List<StructureInstance<AppEnvironmentVariable>> MakeEnvironmentVariableList (
		TaskLoggingHelper log,
		IDictionary<string, string>? environmentVariables,
		LlvmIrStringBlob envVarsBlob,
		StructureInfo? appEnvironmentVariableStructureInfo)
	{
		var ret = new List<StructureInstance<AppEnvironmentVariable>> ();
		if (environmentVariables == null || environmentVariables.Count == 0) {
			return ret;
		}

		foreach (var kvp in environmentVariables) {
			string? name = kvp.Key.Trim ();
			if (String.IsNullOrEmpty (name)) {
				log.LogDebugMessage ($"Not adding environment variable without a name. Value: '{kvp.Value}'");
				continue;
			}
			(int nameOffset, int _) = envVarsBlob.Add (name);
			(int valueOffset, int _) = envVarsBlob.Add (kvp.Value);

			var appEnvVar = new LlvmIrHelpers.AppEnvironmentVariable {
				Name = name,
				Value = kvp.Value,

				name_index = (uint)nameOffset,
				value_index = (uint)valueOffset,
			};
			ret.Add (new StructureInstance<LlvmIrHelpers.AppEnvironmentVariable> (appEnvironmentVariableStructureInfo, appEnvVar));
		}

		return ret;
	}
}
