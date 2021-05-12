using System;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class Step_InstallNDKBinutils : Step
	{
		AndroidToolchainComponentType dependencyTypeToInstall;

		public Step_InstallNDKBinutils (AndroidToolchainComponentType dependencyTypeToInstall = AndroidToolchainComponentType.All)
			: base ("Install host NDK binutils")
		{
			this.dependencyTypeToInstall = dependencyTypeToInstall;
		}

#pragma warning disable CS1998
		protected override async Task<bool> Execute (Context context)
		{
			// Ignore copying if not installing the NDK
			if (!dependencyTypeToInstall.HasFlag (AndroidToolchainComponentType.BuildDependency)) {
				Log.DebugLine ("NDK is not being installed, binutils installation skipped.");
				return true;
			}

			string ndkRoot = context.Properties.GetRequiredValue (KnownProperties.AndroidNdkDirectory);

			string sourceDirectory = Configurables.Paths.AndroidToolchainBinDirectory;
			string destinationDirectory = Configurables.Paths.HostBinutilsInstallDir;

			Log.StatusLine ("Copying host binutils:");
			foreach (var kvp in Configurables.Defaults.AndroidToolchainPrefixes) {
				string archPrefix = kvp.Value;
				foreach (NDKTool ndkTool in Configurables.Defaults.NDKTools) {
					string sourcePath = context.OS.AppendExecutableExtension (Path.Combine (sourceDirectory, $"{archPrefix}-{ndkTool.Name}"));
					string destName = ndkTool.DestinationName.Length == 0 ? ndkTool.Name : ndkTool.DestinationName;
					string destPath = context.OS.AppendExecutableExtension (Path.Combine (destinationDirectory, $"{archPrefix}-{destName}"));

					Log.Status ($"  {context.Characters.Bullet} {Path.GetFileName (sourcePath)} ");
					Log.Status ($"{context.Characters.RightArrow}", ConsoleColor.Cyan);
					Log.StatusLine ($" {Utilities.GetRelativePath (BuildPaths.XamarinAndroidSourceRoot, destPath)}");
					Utilities.CopyFile (sourcePath, destPath);
				}
			}

			return true;
		}
#pragma warning restore CS1998
	}
}
