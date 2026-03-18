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
		throw new NotImplementedException ();
	}
}
