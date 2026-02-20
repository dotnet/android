using System;
using System.Text;

namespace ApplicationUtility;

[AspectReporter (typeof (AssemblyStoreSharedLibrary))]
class AssemblyStoreSharedLibraryReporter : SharedLibraryReporter
{
	protected override string AspectName => AssemblyStoreSharedLibrary.AspectName;
	protected override string LibraryKind => "Assembly store shared library";

	public AssemblyStoreSharedLibraryReporter (SharedLibrary library, MarkdownDocument doc)
		: base (library, doc)
	{}

	protected override void DoListReport ()
	{
		base.DoListReport ();
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

		AddSubsectionBanner ("Assembly Store");
		Reporter.Report (storeLib.AssemblyStore, plainTextRendering: false, ReportForm.Subsection, ReportDoc);
	}
}
