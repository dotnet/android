using System;
using System.Collections.Generic;
using System.IO;

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

		// It's nicer to present the list sorted alphabetically.
		var assemblies = new SortedDictionary<string, ApplicationAssembly> (StringComparer.OrdinalIgnoreCase);
		foreach (var kvp in store.Assemblies) {
			assemblies.Add (GetModifiedSatelliteName (kvp.Value), kvp.Value);
		}

		ReportDoc.BeginList ();
		foreach (var kvp in assemblies) {
			ApplicationAssembly asm = kvp.Value;

			ReportDoc.StartListItem (asm.FullName);
			ReportDoc.BeginList ();

			ReportDoc.AddLabeledListItem ("Size", Utilities.SizeToString (asm.Size));
			AddYesNoListItem ("Compressed", asm.IsCompressed);
			if (asm.IsCompressed) {
				ReportDoc.AddLabeledListItem ("Compressed size", Utilities.SizeToString (asm.CompressedSize));
			}

			bool hasPdb = store.PDBs.TryGetValue (Path.ChangeExtension (asm.Name, ".pdb"), out AssemblyPdb? pdb) && pdb != null;
			ReportDoc.AddLabeledListItem ("Debug info (PDB) present", YesNo (hasPdb));
			if (hasPdb) {
				ReportDoc.AddLabeledListItem ("PDB size", Utilities.SizeToString (pdb!.Size));
			}

			if (asm.IsRTR) {
				ReportDoc.AddLabeledListItem ("ReadyToRun image", "yes");
				ReportDoc.AddLabeledListItem ("RTR target machine", asm.RTRMachine.ToString ());
				ReportDoc.AddLabeledListItem ("RTR target operating system", asm.RTROS.ToString ());
			}

			if (asm.IsSatellite) {
				ReportDoc.AddLabeledListItem ("Satellite", "yes");
				ReportDoc.AddLabeledListItem ("Culture", Utilities.GetCultureInfo (asm.Culture));
			}

			ReportDoc.AddLabeledListItem ("Name hash", $"0x{asm.NameHash:x}");
			AddYesNoListItem ("Ignore on load", asm.IgnoreOnLoad);

			ReportDoc.EndList ().EndListItem (appendLine: false);
		}
		ReportDoc.EndList ().EndListItem ();

		string GetModifiedSatelliteName (ApplicationAssembly asm)
		{
			if (!asm.IsSatellite) {
				return asm.Name;
			}

			return $"{asm.Name} ({asm.Culture})";
		}
	}
}
