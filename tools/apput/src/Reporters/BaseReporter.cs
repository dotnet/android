using System;
using System.Collections.Generic;
using System.Linq;

using Xamarin.Android.Tools;

namespace ApplicationUtility;

/// <summary>
/// Abstract base class for all aspect reporters. Handles common report structure (headings, sections)
/// and delegates to subclasses for aspect-specific content via <see cref="DoReport"/>.
/// </summary>
abstract class BaseReporter : IReporter
{
	protected enum Countable
	{
		Architecture,
		Assembly,
		AssemblyStore,
		Permission,
		SharedLibrary,
	}

	const string NativeArchitectureLabel = "Native target architecture";
	const string NativeArchitecturesLabel = NativeArchitectureLabel + "s";
	const string TargetArchitectureLabel = "Target architecture";

	static readonly Dictionary<Countable, (string singular, string plural)> Countables = new () {
		{ Countable.Architecture, ("architecture", "architectures") },
		{ Countable.Assembly, ("assembly", "assemblies") },
		{ Countable.AssemblyStore, ("assembly store", "assembly stores") },
		{ Countable.Permission, ("permission", "permissions") },
		{ Countable.SharedLibrary, ("library", "libraries") },
	};

	protected abstract string AspectName { get; }
	protected abstract string ShortDescription { get; }

	protected MarkdownDocument ReportDoc { get; }

	protected BaseReporter (MarkdownDocument doc)
	{
		ReportDoc = doc;
	}

	public void Report (ReportForm form, uint sectionLevel = 1)
	{
		bool standalone = form == ReportForm.Standalone;

		if (standalone) {
			AddSection ($"{AspectName} ({ShortDescription})");
		}

		DoReport (form, sectionLevel);

		if (standalone) {
			ReportDoc.AddNewline ();
		}
	}

	protected abstract void DoReport (ReportForm form, uint sectionLevel);

	protected MarkdownDocument AddSection (string text, uint level = 1) => ReportDoc.AddHeading (level, text).AddNewline ();

	protected MarkdownDocument AddLabeledItem (string label, string text, bool appendNewline = true)
	{
		ReportDoc
		    .AddText ($"{label}:", MarkdownTextStyle.Bold)
		    .AddText ($" {text}");

		if (appendNewline) {
			// in Markdown, a forced line break is two or more spaces
			return ReportDoc.AddText ("  ").AddNewline ();
		}

		return ReportDoc;
	}

	protected MarkdownDocument AddAspectDesc (string text)
	{
		ReportDoc.AddHeading (1, "Generic aspect information");
		ReportDoc.AddNewline ();
		return AddLabeledItem ("Aspect type", text);
	}

	protected MarkdownDocument AddNativeArchListItem (NativeArchitecture arch)
	{
		ReportDoc.AddLabeledListItem (NativeArchitectureLabel, arch.ToString ());
		return ReportDoc;
	}

	protected MarkdownDocument AddTargetArchItem (NativeArchitecture arch, bool appendNewline = true)
	{
		AddLabeledItem (TargetArchitectureLabel, arch.ToString (), appendNewline);
		return ReportDoc;
	}

	protected void AddNativeArchDesc (NativeArchitecture arch)
	{
		AddLabeledItem (NativeArchitectureLabel, arch.ToString ());
	}

	protected void AddNativeArchDesc (ICollection<NativeArchitecture> arches)
	{
		if (arches.Count == 1) {
			AddNativeArchDesc (arches.First ());
			return;
		}

		if (arches.Count == 0) {
			ReportDoc.AddText ("None", MarkdownTextStyle.Bold);
			return;
		}

		var architectures = new List<string> ();
		foreach (NativeArchitecture arch in arches) {
			architectures.Add (arch.ToString ());
		}

		AddLabeledItem (NativeArchitecturesLabel, String.Join (", ", architectures));
	}

	protected MarkdownDocument AddYesNo (string label, bool value, bool appendNewLine = true) => AddLabeledItem (label, YesNo (value), appendNewLine);

	protected MarkdownDocument AddYesNoListItem (string label, bool value, bool appendNewLine = true) => ReportDoc.AddLabeledListItem (label, YesNo (value), appendLine: appendNewLine);

	protected MarkdownDocument AddText (string text, MarkdownTextStyle style = MarkdownTextStyle.Plain, bool addIndent = true)
	{
		return ReportDoc.AddText (text, style, addIndent);
	}

	protected MarkdownDocument AddParagraph ()
	{
		ReportDoc.AddNewline ();
		return ReportDoc.AddNewline ();
	}

	protected string GetCountable (Countable countable, ulong count)
	{
		if (!Countables.TryGetValue (countable, out (string singular, string plural) forms)) {
			throw new InvalidOperationException ($"Internal error: unsupported countable {countable}");
		}

		return count == 1 ? forms.singular : forms.plural;
	}

	// Somehow I doubt we'll have more than Int64.MaxValue items of any kind... :)
	protected string GetCountable (Countable countable, long count) => GetCountable (countable, (ulong)count);
	protected string YesNo (bool yes) => yes ? "yes" : "no";
	protected string ValueOrNone (string? s) => String.IsNullOrEmpty (s) ? "none" : s;
}
