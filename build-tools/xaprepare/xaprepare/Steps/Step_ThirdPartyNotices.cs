using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class Step_ThirdPartyNotices : Step
	{
		static readonly Dictionary<ThirdPartyLicenseType, string> tpnBlurbs = new Dictionary <ThirdPartyLicenseType, string> {
			{ ThirdPartyLicenseType.MicrosoftOSS,
			  @"xamarin-android

THIRD - PARTY SOFTWARE NOTICES AND INFORMATION
Do Not Translate or Localize

This project incorporates components from the projects listed below.
The original copyright notices and the licenses under which Microsoft
received such components are set forth below.
Microsoft reserves all rights not expressly granted herein, whether by
implication, estoppel or otherwise."},

			{ ThirdPartyLicenseType.Foundation,
			  @"xamarin-android uses third-party libraries or other resources that may be
distributed under licenses different than the xamarin-android software.

Attributions and license notices for test cases originally authored by
third parties can be found in the respective test directories.

In the event that we accidentally failed to list a required notice, please
bring it to our attention. Post an issue or email us:

           dotnet@microsoft.com

The attached notices are provided for information only."},

			{ ThirdPartyLicenseType.Commercial,
			  @"xamarin-android

THIRD - PARTY SOFTWARE NOTICES AND INFORMATION
Do Not Translate or Localize

Xamarin-Android incorporates components from the projects listed below.
Microsoft licenses these components to you under Microsoft’s licensing
terms, except that components licensed under open source licenses
requiring that such components remain under their original license are
being made available to you under their original licensing terms.
The original copyright notices and the licenses under which Microsoft
received such components are set forth below for informational purposes.
Microsoft reserves all rights not expressly granted herein, whether by
implication, estoppel or otherwise."			}
		};

		public Step_ThirdPartyNotices ()
			: base ("Preparing Third Party Notices")
		{}

#pragma warning disable CS1998
		protected override async Task<bool> Execute (Context context)
		{
			GenerateThirdPartyNotices (Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "THIRD-PARTY-NOTICES.TXT"),
			                           ThirdPartyLicenseType.Foundation,
			                           includeExternalDeps: false,
			                           includeBuildDeps: true);
			Log.StatusLine ();
			GenerateThirdPartyNotices (Path.Combine (context.XAInstallPrefix, "THIRD-PARTY-NOTICES.TXT"),
			                           ThirdPartyLicenseType.MicrosoftOSS,
			                           includeExternalDeps: true,
			                           includeBuildDeps: false);

			return true;
		}
#pragma warning restore CS1998

		void GenerateThirdPartyNotices (string outputPath, ThirdPartyLicenseType licenseType, bool includeExternalDeps, bool includeBuildDeps)
		{
			List<Type> types = Utilities.GetTypesWithCustomAttribute<TPNAttribute> ();
			if (types.Count == 0) {
				Log.StatusLine ("No Third Party Notice entries found", ConsoleColor.Gray);
				return;
			}

			var licenses = new SortedDictionary <string, ThirdPartyNotice> (StringComparer.OrdinalIgnoreCase);
			foreach (Type type in types) {
				EnsureValidTPNType (type);

				if (type.IsSubclassOf (typeof (ThirdPartyNoticeGroup))) {
					ProcessTPN (licenses, Activator.CreateInstance (type) as ThirdPartyNoticeGroup, includeExternalDeps, includeBuildDeps);
					continue;
				}

				if (type.IsSubclassOf (typeof (ThirdPartyNotice))) {
					ProcessTPN (licenses, Activator.CreateInstance (type) as ThirdPartyNotice, includeExternalDeps, includeBuildDeps);
					continue;
				}

				throw new NotSupportedException ($"ThirdPartyNotice type {type.FullName} not supported");
			}

			if (licenses.Count == 0)
				return;

			string? blurb;
			if (!tpnBlurbs.TryGetValue (licenseType, out blurb) || blurb == null)
				throw new InvalidOperationException ($"Unknown license type {licenseType}");

			using (StreamWriter sw = Utilities.OpenStreamWriter (outputPath)) {
				Log.StatusLine ($" Generating: {Utilities.GetRelativePath (BuildPaths.XamarinAndroidSourceRoot, outputPath)}", ConsoleColor.Gray);
				Log.DebugLine ($"Full path: {outputPath}");

				sw.WriteLine (blurb);
				sw.WriteLine ();

				uint i = 0;
				int pad = licenses.Count >= 10 ? 4 : 3;
				foreach (var kvp in licenses) {
					string name = kvp.Key;
					ThirdPartyNotice tpn = kvp.Value;

					sw.Write ($"{++i}.".PadRight (pad));
					sw.WriteLine ($"{name} ({tpn.SourceUrl})");
				}
				sw.WriteLine ();

				foreach (string key in licenses.Keys) {
					ThirdPartyNotice tpn = licenses [key];

					string heading = $"%% {tpn.Name} NOTICES AND INFORMATION BEGIN HERE";
					string underline = "=".PadRight (heading.Length, '=');
					sw.WriteLine (heading);
					sw.WriteLine (underline);
					if (tpn.LicenseText.Length > 0)
						sw.WriteLine (tpn.LicenseText.TrimStart ());
					else
						sw.WriteLine (FetchTPNLicense (tpn.LicenseFile));
					sw.WriteLine ();
					sw.WriteLine (underline);
					sw.WriteLine ($"END OF {tpn.Name} NOTICES AND INFORMATION");
					sw.WriteLine ();
				}

				sw.Flush ();
			}
		}

		string FetchTPNLicense (string relativeFilePath)
		{
			string path = Path.Combine (BuildPaths.XamarinAndroidSourceRoot, relativeFilePath);
			if (!File.Exists (path))
				throw new InvalidOperationException ($"License file {path} does not exist");

			return File.ReadAllText (path);
		}

		void EnsureValidTPNType (Type type)
		{
			if (!type.IsClass || type.IsAbstract)
				throw new InvalidOperationException ($"TPN type must be a non-abstract class ({type})");
		}

		void ProcessTPN (SortedDictionary <string, ThirdPartyNotice> licenses, ThirdPartyNotice? tpn, bool includeExternalDeps, bool includeBuildDeps)
		{
			if (tpn == null)
				throw new ArgumentNullException (nameof (tpn));

			tpn.EnsureValid ();
			if (!tpn.Include (includeExternalDeps, includeBuildDeps))
				return;

			Log.StatusLine ($"  {Context.Instance.Characters.Bullet} Processing: ", tpn.Name, ConsoleColor.Gray, ConsoleColor.White);

			if (licenses.ContainsKey (tpn.Name)) {
				Log.InfoLine ($"Duplicate Third Party Notice '{tpn.Name}' (old class: {licenses [tpn.Name]}; new class: {tpn})");
				return;
			}

			licenses.Add (tpn.Name, tpn);
		}

		void ProcessTPN (SortedDictionary <string, ThirdPartyNotice> licenses, ThirdPartyNoticeGroup? tpng, bool includeExternalDeps, bool includeBuildDeps)
		{
			if (tpng == null)
				throw new ArgumentNullException (nameof (tpng));

			if (tpng.Notices == null || tpng.Notices.All (n => n == null))
				throw new InvalidOperationException ($"TPN group {tpng} without notices");

			if (!tpng.Include (includeExternalDeps, includeBuildDeps))
				return;

			foreach (ThirdPartyNotice tpn in tpng.Notices) {
				if (tpn == null)
					throw new InvalidOperationException ($"TPN group {tpng} contains a null notice");
				if (tpn is ThirdPartyNoticeGroup tpg)
					ProcessTPN (licenses, tpg, includeExternalDeps, includeBuildDeps);
				else
					ProcessTPN (licenses, tpn, includeExternalDeps, includeBuildDeps);
			}
		}
	}
}
