using System;

namespace ApplicationUtility;

/// <summary>
/// Generates a report for a Portable PDB file associated with a .NET assembly. stored in a shared library.
/// </summary>
[AspectReporter (typeof (AssemblyPdbSharedLibrary))]
class AssemblyPdbSharedLibraryReporter : SharedLibraryReporter
{
	protected override string AspectName => "AssemblyPdbSharedLibrary";
	protected override string ShortDescription => "PDB database stored in a shared library";

	public AssemblyPdbSharedLibraryReporter (SharedLibrary pdbLib, MarkdownDocument doc)
		: base (pdbLib, doc)
	{}

	protected override void DoListReport (bool startWithNewLine = true)
	{
		base.DoListReport (startWithNewLine);
		ReportAssemblyPdb ();
	}

	protected override void DoStandaloneReport ()
	{
		base.DoStandaloneReport ();
		ReportAssemblyPdb ();
	}

	void ReportAssemblyPdb ()
	{
		var pdbLib = Library as AssemblyPdbSharedLibrary;
		if (pdbLib == null) {
			throw new InvalidOperationException ($"Internal error: expected instance of {nameof (AssemblyPdbSharedLibrary)}, got {Library.GetType ()} instead.");
		}

		AddSection ("PDB");
		Reporter.Report (pdbLib.PDB, plainTextRendering: false, ReportForm.Subsection, ReportDoc);
	}
}
