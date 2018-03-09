using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public class CreateThirdPartyNotices : Task
	{
		[Required]
		public  ITaskItem[]         Notices             { get; set; }

		[Required]
		public  ITaskItem           FileName            { get; set; }

		public  string              LicenseType         { get; set; }

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (CreateThirdPartyNotices)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (FileName)}: {FileName}");

			var notices = Notices
				.Distinct (NoticesComparer)
				.OrderBy (e => e.ItemSpec, StringComparer.OrdinalIgnoreCase)
				.ToList ();

			var contents    = new StringWriter ();
			WriteHeader (contents);

			int countWidth  = notices.Count.ToString ().Length + 1;
			var countFormat = "{0,-" + countWidth + "}";
			for (int i = 0; i < notices.Count; ++i) {
				var count   = string.Format (countFormat, (i + 1) + ".");
				var notice  = notices [i];
				var version = notice.GetMetadata ("Version");
				if (!string.IsNullOrEmpty (version)) {
					version = $" version {version}";
				}
				contents.WriteLine ($"{count} {notice.ItemSpec}{version} ({notice.GetMetadata ("SourceUrl")})");
			}
			contents.WriteLine ();

			foreach (var notice in notices) {
				contents.WriteLine ();
				contents.WriteLine ($"%% {notice.ItemSpec} NOTICES AND INFORMATION BEGIN HERE");
				contents.WriteLine ("=========================================");

				var licenseText = notice.GetMetadata ("LicenseText");
				WriteLicense (contents, licenseText);

				var licenseFile = notice.GetMetadata ("LicenseFile");
				WriteLicenseFromFile (contents, licenseFile);

				contents.WriteLine ("=========================================");
				contents.WriteLine ($"END OF {notice.ItemSpec} NOTICES AND INFORMATION");
			}

			Directory.CreateDirectory (Path.GetDirectoryName (FileName.ItemSpec));

			var current = File.Exists (FileName.ItemSpec) ? File.ReadAllText (FileName.ItemSpec) : null;
			if (current != contents.ToString ()) {
				File.WriteAllText (FileName.ItemSpec, contents.ToString ());
			}

			return !Log.HasLoggedErrors;
		}

		void WriteHeader (TextWriter contents)
		{
			switch (LicenseType?.ToLowerInvariant ()) {
				case "foundation":
					WriteFoundationHeader (contents);
					break;
				case "microsoft-commercial":
					WriteMicrosoftCommercialHeader (contents);
					break;
				case "microsoft-oss":
				default:
					WriteMicrosoftOSSHeader (contents);
					break;
			}
		}

		void WriteFoundationHeader (TextWriter contents)
		{
			contents.WriteLine ("xamarin-android uses third-party libraries or other resources that may be");
			contents.WriteLine ("distributed under licenses different than the xamarin-android software.");
			contents.WriteLine ();
			contents.WriteLine ("Attributions and license notices for test cases originally authored by");
			contents.WriteLine ("third parties can be found in the respective test directories.");
			contents.WriteLine ();
			contents.WriteLine ("In the event that we accidentally failed to list a required notice, please");
			contents.WriteLine ("bring it to our attention. Post an issue or email us:");
			contents.WriteLine ();
			contents.WriteLine ("           dotnet@microsoft.com");
			contents.WriteLine ();
			contents.WriteLine ("The attached notices are provided for information only.");
			contents.WriteLine ();
		}

		void WriteMicrosoftCommercialHeader (TextWriter contents)
		{
			contents.WriteLine ("xamarin-android");
			contents.WriteLine ();
			contents.WriteLine ("THIRD - PARTY SOFTWARE NOTICES AND INFORMATION");
			contents.WriteLine ("Do Not Translate or Localize");
			contents.WriteLine ();
			contents.WriteLine ("Xamarin-Android incorporates components from the projects listed below.");
			contents.WriteLine ("Microsoft licenses these components to you under Microsoft’s licensing");
			contents.WriteLine ("terms, except that components licensed under open source licenses");
			contents.WriteLine ("requiring that such components remain under their original license are");
			contents.WriteLine ("being made available to you under their original licensing terms.");
			contents.WriteLine ("The original copyright notices and the licenses under which Microsoft");
			contents.WriteLine ("received such components are set forth below for informational purposes.");
			contents.WriteLine ("Microsoft reserves all rights not expressly granted herein, whether by");
			contents.WriteLine ("implication, estoppel or otherwise.");
			contents.WriteLine ();
		}

		void WriteMicrosoftOSSHeader (TextWriter contents)
		{
			contents.WriteLine ("xamarin-android");
			contents.WriteLine ();
			contents.WriteLine ("THIRD - PARTY SOFTWARE NOTICES AND INFORMATION");
			contents.WriteLine ("Do Not Translate or Localize");
			contents.WriteLine ();
			contents.WriteLine ("This project incorporates components from the projects listed below.");
			contents.WriteLine ("The original copyright notices and the licenses under which Microsoft");
			contents.WriteLine ("received such components are set forth below.");
			contents.WriteLine ("Microsoft reserves all rights not expressly granted herein, whether by");
			contents.WriteLine ("implication, estoppel or otherwise.");
			contents.WriteLine ();
		}

		void WriteLicense (TextWriter contents, string licenseText)
		{
			if (string.IsNullOrEmpty (licenseText)) {
				return;
			}
			using (var license = new StringReader (licenseText)) {
				string line;
				while ((line = license.ReadLine ()) != null) {
					contents.WriteLine (line.Trim ());
				}
			}
		}

		void WriteLicenseFromFile (TextWriter contents, string licenseFile)
		{
			if (string.IsNullOrEmpty (licenseFile)) {
				return;
			}
			foreach (var line in File.ReadLines (licenseFile)) {
				contents.WriteLine (line);
			}
		}

		static  readonly    TaskItemComparer            NoticesComparer         = new TaskItemComparer ();

		class TaskItemComparer : IEqualityComparer<ITaskItem> {

			public bool Equals (ITaskItem a, ITaskItem b)
			{
				return string.Equals (a.ItemSpec, b.ItemSpec, StringComparison.OrdinalIgnoreCase);
			}

			public int GetHashCode (ITaskItem value)
			{
				return value.ItemSpec.GetHashCode ();
			}
		}
	}
}

