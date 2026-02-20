using System;

namespace ApplicationUtility;

[AspectReporter (typeof (AssemblyStore))]
class AssemblyStoreReporter : BaseReporter
{
	protected override string AspectName => AssemblyStore.AspectName;
	protected override string ShortDescription => "Assembly store";

	readonly AssemblyStore store;

	public AssemblyStoreReporter (AssemblyStore store, MarkdownDocument doc)
		: base (doc)
	{
		this.store = store;
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
				throw new NotSupportedException ($"Unsupported report form '{form}'");
		}
	}

	void DoStandaloneReport ()
	{
		throw new NotImplementedException ();
	}

	void DoSubsectionReport (uint sectionLevel)
	{
		AddTargetArchItem (store.Architecture);
		AddLabeledItem ("Number of assemblies", store.NumberOfAssemblies.ToString ());

		AddSubsectionBanner ("Assemblies", level: sectionLevel + 1);

		// TODO: probably want to sort them alphabetically
		ReportDoc.BeginList ();
		foreach (var kvp in store.Assemblies) {
			ApplicationAssembly asm = kvp.Value;

			ReportDoc.AddListItem (asm.Name);
			// ReportDoc.BeginList ();
			// AddYesNo ("Compressed", asm.IsCompressed);
			// ReportDoc.AddLabeledListItem ("Size", $"{asm.Size}");
			// if (asm.IsCompressed) {
			// 	ReportDoc.AddLabeledListItem ("Compressed size", $"{asm.CompressedSize}");
			// }
			// ReportDoc.AddLabeledListItem ("Name hash", $"0x{asm.NameHash:x}");
			// AddYesNo ("Ignore on load", asm.IgnoreOnLoad);
			// ReportDoc.EndList ();
		}
		ReportDoc.EndList ().EndListItem ();
	}
}
