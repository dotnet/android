#nullable disable

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks {

	internal partial class ManifestDocument
	{
		const int maxVersionCode = 2100000000;

		static readonly Regex versionCodeRegex = new Regex ("\\{(?<key>([A-Za-z]+)):?[D0-9]*[\\}]", RegexOptions.Compiled);

		public string TargetSdkVersion { get; set; }
		public string MinSdkVersion { get; set; }

		/// <summary>
		/// NOTE: this property modifies the underlying XDocument
		/// </summary>
		public string VersionCode {
			get {
				XAttribute attr = doc.Root.Attribute (versionCodeAttributeName);
				if (attr != null) {
					string code = attr.Value;
					if (!string.IsNullOrEmpty (code))
						return code;
				}
				return "1";
			}
			set {
				doc.Root.SetAttributeValue (versionCodeAttributeName, value);
			}
		}

		// If MinSdkVersionName can't be parsed, set it to XABuildConfig.AndroidMinimumDotNetApiLevel
		string TryParseMinSdkVersionName ()
		{
			int minSdkVersion;
			if (!int.TryParse (MinSdkVersionName, out minSdkVersion))
				minSdkVersion = XABuildConfig.AndroidMinimumDotNetApiLevel.Major;
			return minSdkVersion.ToString ();
		}

		public string GetMinimumSdk () {
			var minAttr = doc.Root.Element ("uses-sdk")?.Attribute (androidNs + "minSdkVersion");
			if (minAttr == null) {
				return TryParseMinSdkVersionName ();
			}
			return minAttr.Value;
		}

		// TargetSdkVersion and MinSdkVersion are already resolved to integer API level
		// strings (e.g. "36") by the manifest generator. Don't round-trip through
		// GetIdFromApiLevel() because when multiple API versions share the same ApiLevel
		// (e.g. 36.0 and 36.1 both have ApiLevel=36), it can return the wrong Id
		// (e.g. "36.1" instead of "36").
		string TargetSdkVersionName => TargetSdkVersion;

		string MinSdkVersionName =>
			string.IsNullOrEmpty (MinSdkVersion) ?
				TargetSdkVersionName :
				MinSdkVersion;

		static int GetAbiCode (string abi)
		{
			switch (abi) {
			case "armeabi-v7a":
				return 2;
			case "x86":
				return 3;
			case "arm64-v8a":
				return 4;
			case "x86_64":
				return 5;
			default:
				throw new ArgumentOutOfRangeException ("abi", "unsupported ABI");
			}
		}

		public void SetAbi (string abi)
		{
			int code = 1;
			if (!string.IsNullOrEmpty (VersionCode)) {
				code = Convert.ToInt32 (VersionCode);
				if (code > maxVersionCode || code < 0)
					throw new ArgumentOutOfRangeException ("VersionCode", $"VersionCode is outside 0, {maxVersionCode} interval");
			}
			code |= GetAbiCode (abi) << 16;
			VersionCode = code.ToString ();
		}

		public bool ValidateVersionCode (out string error, out string errorCode)
		{
			int code;
			error = errorCode = "";
			if (!int.TryParse (VersionCode, out code)) {
				error = string.Format (Properties.Resources.XA0003, VersionCode);
				errorCode = "XA0003";
				return false;
			}
			if (code > maxVersionCode || code < 0) {
				error = string.Format (Properties.Resources.XA0004, code, maxVersionCode);
				errorCode = "XA0004";
				return false;
			}
			return true;
		}

		public void CalculateVersionCode (string currentAbi, string versionCodePattern, string versionCodeProperties)
		{
			var kvp = new Dictionary<string, int> ();
			foreach (var item in versionCodeProperties?.Split (new char [] { ';', ':' }) ?? []) {
				var keyValue = item.Split (new char [] { '=' });
				int val;
				if (!int.TryParse (keyValue [1], out val))
					continue;
				kvp.Add (keyValue [0], val);
			}
			if (!kvp.ContainsKey ("abi") && !string.IsNullOrEmpty (currentAbi))
				kvp.Add ("abi", GetAbiCode (currentAbi));
			if (!kvp.ContainsKey ("versionCode")) {
				if (int.TryParse (VersionCode, out int parsedCode)) {
					kvp.Add ("versionCode", parsedCode);
				} else {
					throw new ArgumentOutOfRangeException ("VersionCode", $"VersionCode {VersionCode} is invalid. It must be an integer value.");
				}
			}
			if (!kvp.ContainsKey ("minSDK") &&
					int.TryParse (GetMinimumSdk (), out var minSdk)) {
				kvp.Add ("minSDK", minSdk);
			}
			var versionCode = new StringBuilder ();
			foreach (Match match in versionCodeRegex.Matches (versionCodePattern)) {
				var key = match.Groups ["key"].Value;
				var format = match.Value.Replace (key, "0");
				if (!kvp.ContainsKey (key))
					continue;
				versionCode.AppendFormat (format, kvp [key]);
			}
			VersionCode = versionCode.ToString ().TrimStart ('0');
		}
	}
}
