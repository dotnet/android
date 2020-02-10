using System;
using System.IO;
using System.Collections.Generic;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class PrepareAbiItems : AndroidTask
	{
		const string ArmV7a = "armeabi-v7a";
		const string TypeMapBase = "typemaps";
		const string EnvBase = "environment";

		public override string TaskPrefix => "PAI";

		[Required]
		public string [] BuildTargetAbis { get; set; }

		[Required]
		public string NativeSourcesDir { get; set; }

		[Required]
		public bool TypeMapMode { get; set; }

		[Output]
		public ITaskItem[] AssemblySources { get; set; }

		[Output]
		public ITaskItem[] AssemblyIncludes { get; set; }

		public override bool RunTask ()
		{
			var sources = new List<ITaskItem> ();
			var includes = new List<ITaskItem> ();
			bool haveSharedSource = false;
			bool haveArmV7SharedSource = false;
			string baseName = TypeMapMode ? TypeMapBase : EnvBase;
			TaskItem item;
			foreach (string abi in BuildTargetAbis) {
				if (TypeMapMode) {
					if (String.Compare (ArmV7a, abi, StringComparison.Ordinal) == 0)
						haveArmV7SharedSource = true;
					else
						haveSharedSource = true;
				}

				item = new TaskItem (Path.Combine (NativeSourcesDir, $"{baseName}.{abi}.s"));
				item.SetMetadata ("abi", abi);
				sources.Add (item);

				if (!TypeMapMode)
					continue;

				item = new TaskItem (Path.Combine (NativeSourcesDir, $"{baseName}.{abi}-managed.inc"));
				item.SetMetadata ("abi", abi);
				includes.Add (item);
			}

			if (haveArmV7SharedSource) {
				item = new TaskItem (Path.Combine (NativeSourcesDir, $"{baseName}.{ArmV7a}-shared.inc"));
				item.SetMetadata ("abi", ArmV7a);
				includes.Add (item);
			}

			if (haveSharedSource) {
				item = new TaskItem (Path.Combine (NativeSourcesDir, $"{baseName}.shared.inc"));
				includes.Add (item);
			}

			AssemblySources = sources.ToArray ();
			AssemblyIncludes = includes.ToArray ();
			return !Log.HasLoggedErrors;
		}
	}
}
