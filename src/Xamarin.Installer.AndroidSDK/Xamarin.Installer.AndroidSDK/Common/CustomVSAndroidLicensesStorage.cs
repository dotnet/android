using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Xamarin.Installer.AndroidSDK.Common
{
	public class CustomVSAndroidLicensesStorage : AndroidLicensesStorage
	{
		protected override string GetLicensesPath(string androidSdkPath)
		{
			if (string.IsNullOrWhiteSpace(androidSdkPath) || !Directory.Exists(androidSdkPath))
			{
				return null;
			}

			var androidSdkId = GetLicensesLocalStorageIdFor(androidSdkPath);
			var winHome = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			return Path.Combine(winHome, "Xamarin", "Mono for Android", "Licenses", androidSdkId);
		}

		string GetLicensesLocalStorageIdFor(string androidSdkPath)
		{
			using (var hashAlgorithm = SHA256.Create())
			{
				var bytes = Encoding.UTF8.GetBytes(androidSdkPath.ToLowerInvariant());
				var hash = string.Concat(hashAlgorithm.ComputeHash(bytes).Select(x => x.ToString("x2")));
				return hash;
			}
		}

		protected override string GetLicenseHash(License license)
		{
			if (string.IsNullOrEmpty(license?.Text))
			{
				return null;
			}

			var content = string.IsNullOrEmpty(license.Text) ? license.ID : license.Text;
			using (var hashAlgorithm = SHA256.Create())
			{
				var bytes = Encoding.UTF8.GetBytes(content.ToLowerInvariant());
				var hash = string.Concat(hashAlgorithm.ComputeHash(bytes).Select(x => x.ToString("x2")));
				return hash;
			}
		}
	}
}
