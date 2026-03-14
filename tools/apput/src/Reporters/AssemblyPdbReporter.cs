using System;

namespace ApplicationUtility;

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
		throw new NotImplementedException ();
	}
}
