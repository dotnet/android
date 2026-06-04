using System;
using System.IO;

using Xamarin.Installer.AndroidSDK;
using Xamarin.Installer.AndroidSDK.Manager;
using Xamarin.Installer.Common;

namespace Xamarin.Installer.AndroidSDK
{
	public interface ILocalManifestProvider {
		void SaveManifest (string manifest, string fileName = null);
		string GetManifest (string fileName = null);
	}
	class LocalManifestProvider: ILocalManifestProvider
	{
		const string AppName = "AndroidSDKManager";

		const string XAMARIN_MANIFEST_FILE_NAME  = "XamarinManifest.xml";
		const string GOOGLE_MANIFEST_FILE_NAME = "GoogleManifest.xml";

		static string CacheFolder = ConfigManager.ConfigFolder;

		public static LocalManifestProvider CreateGoogleManifestProvider ()
		{
			return new LocalManifestProvider (AndroidManifestType.GoogleV2);
		}

		public static LocalManifestProvider CreateXamarinManifestProvider ()
		{
			return new LocalManifestProvider (AndroidManifestType.Xamarin);
		}
		
		AndroidManifestType manifestType;


		LocalManifestProvider (AndroidManifestType manifestType)
		{
			this.manifestType = manifestType;
			if (manifestType != AndroidManifestType.GoogleV2 && manifestType != AndroidManifestType.Xamarin)
				throw new ArgumentException ("LocalManifestProvider supports GoogleV2 and Xamarin manifest types only", "manifestType");
		}

		public void SaveManifest (string manifest, string fileName = null)
		{
			try {
				if (!Directory.Exists (CacheFolder))
						Directory.CreateDirectory (CacheFolder);

				File.WriteAllText (GetManifestFilePath (fileName), manifest);
			} catch (Exception ex) {
				Logger.Error ($"Failed to save manifest: {ex}");
			}
		}

		public string GetManifest (string fileName = null)
		{
            var manifestFilePath = GetManifestFilePath(fileName);
            if (!File.Exists(manifestFilePath))
            {
                Logger.Info("GetManifest:: manifestFilePath " + manifestFilePath);
                return null;
            }

			
			try {
				return File.ReadAllText (manifestFilePath);
			} catch (Exception ex) {
				Logger.Error ($"Failed to load local manifest: {ex}");
			}
			
			return null;
		}

		string GetManifestFilePath (string fileName = null)
		{
			fileName = fileName ?? ManifestFileName;
			return Path.Combine (CacheFolder, fileName);
		}

		string ManifestFileName {
			get {
				return manifestType == AndroidManifestType.Xamarin ? XAMARIN_MANIFEST_FILE_NAME : GOOGLE_MANIFEST_FILE_NAME;
			}
		}

	}
}