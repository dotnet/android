#nullable enable
using System;
using System.IO;
using System.Collections.Generic;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class PrepareAbiItems : AndroidTask
	{
		const string ArmV7a = "armeabi-v7a";
		const string TypeMapBase = "typemaps";
		const string EnvBase = "environment";
		const string CompressedAssembliesBase = "compressed_assemblies";
		const string JniRemappingBase = "jni_remap";
		const string MarshalMethodsBase = "marshal_methods";
		const string PinvokePreserveBase = "pinvoke_preserve";

		public override string TaskPrefix => "PAI";

		[Required]
		public string [] BuildTargetAbis { get; set; } = [];

		[Required]
		public string NativeSourcesDir { get; set; } = "";

		[Required]
		public string Mode { get; set; } = "";

		[Required]
		public bool Debug { get; set; }

		[Output]
		public ITaskItem[]? AssemblySources { get; set; }

		[Output]
		public ITaskItem[]? AssemblyIncludes { get; set; }

		public override bool RunTask ()
		{
			var sources = new List<ITaskItem> ();
			var includes = new List<ITaskItem> ();
			string baseName;

			if (MonoAndroidHelper.StringEquals ("typemap", Mode, StringComparison.OrdinalIgnoreCase)) {
				baseName = TypeMapBase;
			} else if (MonoAndroidHelper.StringEquals ("environment", Mode, StringComparison.OrdinalIgnoreCase)) {
				baseName = EnvBase;
			} else if (MonoAndroidHelper.StringEquals ("compressed", Mode, StringComparison.OrdinalIgnoreCase)) {
				baseName = CompressedAssembliesBase;
			} else if (MonoAndroidHelper.StringEquals ("jniremap", Mode, StringComparison.OrdinalIgnoreCase)) {
				baseName = JniRemappingBase;
			} else if (MonoAndroidHelper.StringEquals ("marshal_methods", Mode, StringComparison.OrdinalIgnoreCase)) {
				baseName = MarshalMethodsBase;
			} else if (MonoAndroidHelper.StringEquals ("runtime_linking", Mode, StringComparison.OrdinalIgnoreCase)) {
				baseName = PinvokePreserveBase;
			} else {
				Log.LogError ($"Unknown mode: {Mode}");
				return false;
			}

			TaskItem item;
			foreach (string abi in BuildTargetAbis) {
				item = new TaskItem (Path.Combine (NativeSourcesDir, $"{baseName}.{abi}.ll"));
				item.SetMetadata ("abi", abi);
				sources.Add (item);
			}

			AssemblySources = sources.ToArray ();
			AssemblyIncludes = includes.ToArray ();
			return !Log.HasLoggedErrors;
		}
	}
}
