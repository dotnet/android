using System;

namespace ApplicationUtility;

/// <summary>
/// Generates a report for a single .NET application assembly stored in a shared library, including name, size,
/// compression status, architecture, and satellite assembly info.
/// </summary>
[AspectReporter (typeof (AssemblySharedLibrary))]
class AssemblySharedLibraryReporter : SharedLibraryReporter
{
	protected override string AspectName => "AssemblySharedLibrary";
	protected override string ShortDescription => "Managed assembly stored in a shared library";

	public AssemblySharedLibraryReporter (SharedLibrary asmLib, MarkdownDocument doc)
		: base (asmLib, doc)
	{}

	protected override void DoListReport (bool startWithNewLine = true)
	{
		base.DoListReport (startWithNewLine);
		ReportAssembly ();
	}

	protected override void DoStandaloneReport ()
	{
		base.DoStandaloneReport ();
		ReportAssembly ();
	}

	void ReportAssembly ()
	{
		var assemblyLib = Library as AssemblySharedLibrary;
		if (assemblyLib == null) {
			throw new InvalidOperationException ($"Internal error: expected instance of {nameof (AssemblySharedLibrary)}, got {Library.GetType ()} instead.");
		}

		AddSection ("Assembly");
		Reporter.Report (assemblyLib.Assembly, plainTextRendering: false, ReportForm.Subsection, ReportDoc);
	}
}
