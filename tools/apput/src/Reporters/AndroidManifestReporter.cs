using System;

namespace ApplicationUtility;

/// <summary>
/// Generates a report for an Android manifest, including package name, SDK versions,
/// main activity, permissions, debuggable flag, and manifest format.
/// </summary>
[AspectReporter (typeof (AndroidManifest))]
class AndroidManifestReporter : BaseReporter
{
	readonly AndroidManifest manifest;

	protected override string AspectName => "AndroidManifest";
	protected override string ShortDescription => "Android application manifest";

	public AndroidManifestReporter (AndroidManifest manifest, MarkdownDocument doc)
		: base (doc)
	{
		this.manifest = manifest;
	}

	protected override void DoReport (ReportForm form, uint sectionLevel)
	{
		switch (form) {
			case ReportForm.Standalone:
				DoReport_Standalone ();
				break;

			case ReportForm.Subsection:
				DoReport_Subsection (sectionLevel);
				break;

			default:
				throw new NotSupportedException ($"Report form '{form}' is not supported here.");
		}
	}

	void DoReport_Standalone ()
	{
		DoReport_Common (sectionLevel: 0);
	}

	void DoReport_Subsection (uint sectionLevel)
	{
		DoReport_Common (sectionLevel);
	}

	void DoReport_Common (uint sectionLevel)
	{
		AddSection ("General manifest information", sectionLevel + 1);

		AddLabeledItem ("Source format", manifest.Format.ToString ());
		AddLabeledItem ("Package name", ValueOrNone (manifest.PackageName));
		AddLabeledItem ("Main activity", ValueOrNone (manifest.MainActivity));
		AddLabeledItem ("Minimum SDK version", ValueOrNone (manifest.MinSdkVersion));
		AddLabeledItem ("Target SDK version", ValueOrNone (manifest.TargetSdkVersion));

		AddSection ("Full XML contents", sectionLevel + 1);
		AddText ("```xml");
		AddText (manifest.RenderedXML);
		AddText ("```");
	}
}
