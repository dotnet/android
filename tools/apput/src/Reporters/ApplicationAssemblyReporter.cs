using System;

namespace ApplicationUtility;

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
		throw new NotImplementedException ();
	}
}
