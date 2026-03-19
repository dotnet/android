using System;

namespace ApplicationUtility;

/// <summary>
/// Generates a report for a single .NET application assembly, including name, size,
/// compression status, architecture, and satellite assembly info.
/// </summary>
[AspectReporter (typeof (ApplicationAssembly))]
class ApplicationAssemblyReporter : BaseReporter
{
	readonly ApplicationAssembly assembly;

	protected override string AspectName => "Assembly";
	protected override string ShortDescription => "Managed assembly";

	public ApplicationAssemblyReporter (ApplicationAssembly asm, MarkdownDocument doc)
		: base (doc)
	{
		this.assembly = asm;
	}

	protected override void DoReport (ReportForm form, uint sectionLevel)
	{
		switch (form) {
			case ReportForm.Standalone:
				DoStandaloneReport ();
				break;

			case ReportForm.Subsection:
				DoSubsectionReport (sectionLevel);
				break;

			default:
				throw new NotSupportedException ($"Report form '{form}' is not supported.");
		};
	}

	void DoStandaloneReport () => DoReport (1);
	void DoSubsectionReport (uint sectionLevel) => DoReport (sectionLevel);

	void DoReport (uint sectionLevel)
	{
		AddLabeledItem ("Name", assembly.FullName);
		AddLabeledItem ("Architecture", assembly.Architecture.ToString ());
		if (assembly.IsRTR) {
			AddLabeledItem ("ReadyToRun image", "yes");
			AddLabeledItem ("RTR target machine", assembly.RTRMachine.ToString ());
			AddLabeledItem ("RTR target operating system", assembly.RTROS.ToString ());
		}

		if (assembly.IsSatellite) {
			AddLabeledItem ("Satellite", "yes");
			AddLabeledItem ("Culture", Utilities.GetCultureInfo (assembly.Culture));
		}
		AddLabeledItem ("Compressed", YesNo (assembly.IsCompressed));
		if (assembly.IsCompressed) {
			AddLabeledItem ("Compressed size", Utilities.SizeToString (assembly.CompressedSize));
		}
		AddLabeledItem ("Size", Utilities.SizeToString (assembly.Size));

		if (assembly.Container == ApplicationAssemblyContainer.AssemblyStore) {
			ReportDoc.AddLabeledListItem ("Name hash", $"0x{assembly.NameHash:x}");
			ReportDoc.AddLabeledListItem ("Ignore on load", YesNo (assembly.IgnoreOnLoad));
		}
	}
}
