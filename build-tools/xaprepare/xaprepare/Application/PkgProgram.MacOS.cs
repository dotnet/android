using System;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class PkgProgram : Program
	{
		public override bool NeedsSudoToInstall => false;
		public string PackageId                { get; }
		public Uri PackageUrl                  { get; set; }
		protected bool SkipPkgUtilVersionCheck { get; set; }

		public PkgProgram (string name, string packageId, Uri packageUrl = null)
		{
			if (String.IsNullOrEmpty (name))
				throw new ArgumentException ("must not be null or empty", nameof (name));

			if (String.IsNullOrEmpty(packageId))
				throw new ArgumentException ("must not be null or empty", nameof (packageId));

			Name = name;
			PackageId = packageId;
			PackageUrl = packageUrl;
		}

		public override async Task<bool> Install ()
		{
			Context context = Context.Instance;

			if (!context.AutoProvisionUsesSudo) {
				Log.ErrorLine ("Installation of macOS packages requires sudo to be enabled (pass `--auto-provision-uses-sudo=yes` to the bootstrapper)");
				return false;
			}

			if (PackageUrl == null) {
				Log.ErrorLine ($"{Name} is not installed but no URL is provided to download it from. Please make sure to install it before continuing");
				return false;
			}

			(bool success, ulong size) = await Utilities.GetDownloadSize (PackageUrl);
			if (!success) {
				Log.ErrorLine ($"Failed to get download size of {PackageUrl}");
				return false;
			}

			DownloadStatus downloadStatus = Utilities.SetupDownloadStatus (context, size, context.InteractiveSession);
			Log.StatusLine ($"  {context.Characters.Link} {PackageUrl}", ConsoleColor.White);

			string localPath = Path.Combine (context.Properties.GetRequiredValue (KnownProperties.AndroidToolchainCacheDirectory), Path.GetFileName (PackageUrl.LocalPath));
			success = await Utilities.Download (PackageUrl, localPath, downloadStatus);
			if (!success) {
				Log.ErrorLine ($"Failed to download {PackageUrl}");
				return false;
			}

			var runner = new ProcessRunner ("sudo") {
				EchoStandardError = true,
				EchoStandardOutput = true,
				ProcessTimeout = TimeSpan.FromMinutes (10)
			};

			runner.AddArgument ("/usr/sbin/installer");
			runner.AddArgument ("-verbose");
			runner.AddArgument ("-pkg");
			runner.AddQuotedArgument (localPath);
			runner.AddArgument ("-target");
			runner.AddArgument ("/");

			return await Task.Run (() => runner.Run ());
		}

		protected override bool CheckWhetherInstalled ()
		{
			return GetVersion (echoError: false).Result != null;
		}

		protected override async Task<bool> DetermineCurrentVersion ()
		{
			if (SkipPkgUtilVersionCheck)
				return await base.DetermineCurrentVersion ();

			var runner = new PkgutilRunner (Context.Instance);

			Version ver = await GetVersion (echoError: true);
			if (ver == null)
				return false;

			CurrentVersion = ver.ToString ();
			return true;
		}

		async Task<Version> GetVersion (bool echoError)
		{
			var runner = new PkgutilRunner (Context.Instance);
			return await runner.GetPackageVersion (PackageId, echoError: echoError);
		}
	}
}
