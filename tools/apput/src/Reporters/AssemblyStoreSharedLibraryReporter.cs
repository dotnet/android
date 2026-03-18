using System;

namespace ApplicationUtility;

/// <summary>
/// Generates a report for a shared library containing an embedded assembly store.
/// Reports the library metadata as well as the contained assembly store details.
/// </summary>
[AspectReporter (typeof (AssemblyStoreSharedLibrary))]
class AssemblyStoreSharedLibraryReporter : SharedLibraryReporter
{
	protected override string AspectName => Library.AspectName;
	protected override string LibraryKind => "Assembly store shared library";

	public AssemblyStoreSharedLibraryReporter (SharedLibrary library, MarkdownDocument doc)
		: base (library, doc)
	{}

	protected override void DoListReport (bool startWithNewLine = true)
	{
		base.DoListReport (startWithNewLine);
		ReportAssemblyStore ();
	}

	protected override void DoStandaloneReport ()
	{
		base.DoStandaloneReport ();
		ReportAssemblyStore ();
	}

	void ReportAssemblyStore ()
	{
		var storeLib = Library as AssemblyStoreSharedLibrary;
		if (storeLib == null) {
			throw new InvalidOperationException ($"Internal error: expected instance of {nameof (AssemblyStoreSharedLibrary)}, got {Library.GetType ()} instead.");
		}

		AddSection ("Assembly Store");
		Reporter.Report (storeLib.AssemblyStore, plainTextRendering: false, ReportForm.Subsection, ReportDoc);
	}
}
