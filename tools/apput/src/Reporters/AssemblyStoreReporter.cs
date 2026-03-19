using System;

namespace ApplicationUtility;

/// <summary>
/// Generates a report for an assembly store, listing contained assemblies, PDBs, and config files.
/// </summary>
[AspectReporter (typeof (AssemblyStore))]
class AssemblyStoreReporter : BaseReporter
{
	protected override string AspectName => store.AspectName;
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

	void DoStandaloneReport () => DoReport (1);
	void DoSubsectionReport (uint sectionLevel) => DoReport (sectionLevel);

	void DoReport (uint sectionLevel)
	{
		AddTargetArchItem (store.Architecture);
		AddLabeledItem ("Number of assemblies", store.NumberOfAssemblies.ToString ());

		AddSection ("Assemblies", level: sectionLevel + 1);

		// TODO: probably want to sort them alphabetically
		ReportDoc.BeginList ();
		foreach (var kvp in store.Assemblies) {
			ApplicationAssembly asm = kvp.Value;

			ReportDoc.StartListItem (asm.FullName);
			ReportDoc.BeginList ();
			AddYesNoListItem ("Compressed", asm.IsCompressed);
			ReportDoc.AddLabeledListItem ("Size", $"{asm.Size}");
			if (asm.IsCompressed) {
				ReportDoc.AddLabeledListItem ("Compressed size", $"{asm.CompressedSize}");
			}
			ReportDoc.AddLabeledListItem ("Name hash", $"0x{asm.NameHash:x}");
			AddYesNoListItem ("Ignore on load", asm.IgnoreOnLoad);
			ReportDoc.EndList ().EndListItem (appendLine: false);
		}
		ReportDoc.EndList ().EndListItem ();
	}
}
