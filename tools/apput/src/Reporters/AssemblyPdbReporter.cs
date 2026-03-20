using System;

namespace ApplicationUtility;
/// <summary>
/// Generates a report for a Portable PDB file associated with a .NET assembly.
/// </summary>
[AspectReporter (typeof (AssemblyPdb))]
class AssemblyPdbReporter : BaseReporter
{
	readonly AssemblyPdb pdb;

	protected override string AspectName => "PPDB";
	protected override string ShortDescription => "Portable PDB Assembly Debug Data";

	public AssemblyPdbReporter (AssemblyPdb pdb, MarkdownDocument doc)
		: base (doc)
	{
		this.pdb = pdb;
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
		AddLabeledItem ("Name", pdb.Name);
		AddLabeledItem ("Architecture", pdb.Architecture.ToString ());
		AddLabeledItem ("Size", Utilities.SizeToString (pdb.Size));
	}
}
