using System;
using System.Collections.Generic;
using System.Linq;

using Xamarin.Android.Tools;

namespace ApplicationUtility;

// TODO: generate Markdown
// TODO: detect whether we can output colors
// TODO: reporters should be used for all items contained in the aspect being reported on. This requires
//       changes to support different write formats depending on whether an aspect is being reported on
//       in standalone or "embedded" mode.
abstract class BaseReporter : IReporter
{
	protected enum Countable
	{
		Assembly,
		AssemblyStore,
		Permission,
		SharedLibrary,
	}

	const string NativeArchitectureLabel = "Native target architecture";
	const string NativeArchitecturesLabel = NativeArchitectureLabel + "s";

	static readonly Dictionary<Countable, (string singular, string plural)> Countables = new () {
		{ Countable.Assembly, ("assembly", "assemblies") },
		{ Countable.AssemblyStore, ("assembly store", "assembly stores") },
		{ Countable.Permission, ("permission", "permissions") },
		{ Countable.SharedLibrary, ("library", "libraries") },
	};

	protected const ConsoleColor LabelColor = ConsoleColor.Gray;
	protected const ConsoleColor ValidValueColor = ConsoleColor.Green;
	protected const ConsoleColor InvalidValueColor = ConsoleColor.Red;
	protected const ConsoleColor BannerColor = ConsoleColor.Cyan;

	protected abstract string AspectName { get; }
	protected abstract string ShortDescription { get; }

	protected static readonly bool CanUseColor = !Console.IsOutputRedirected;

	protected MarkdownDocument ReportDoc { get; }

	protected BaseReporter (MarkdownDocument doc)
	{
		ReportDoc = doc;
	}

	public void Report ()
	{
		WriteLine (BannerColor, $"# {AspectName} ({ShortDescription})");
		DoReport ();
		WriteLine ();
	}

	protected abstract void DoReport ();

	protected MarkdownDocument AddSection (string text, uint level = 1) => ReportDoc.AddHeading (level, text).AddNewline ();

	protected MarkdownDocument AddLabeledItem (string label, string text, bool appendNewline = true)
	{
		ReportDoc
		    .AddText ($"{label}:", MarkdownTextStyle.Bold)
		    .AddText ($" {text}");

		if (appendNewline) {
			return ReportDoc.AddNewline ();
		}

		return ReportDoc;
	}

	protected MarkdownDocument AddAspectDesc (string text)
	{
		ReportDoc.AddHeading (1, "Generic aspect information");
		ReportDoc.AddNewline ();
		return AddLabeledItem ("Aspect type", text);
	}

	protected void WriteSubsectionBanner (string text)
	{
		WriteLine ();
		WriteLine (BannerColor, $"## {text}");
	}

	protected void WriteAspectDesc (string text)
	{
		WriteItem ("Aspect type", text);
	}

	protected void WriteNativeArch (NativeArchitecture arch)
	{
		WriteItem (NativeArchitectureLabel, arch.ToString ());
	}

	protected void WriteNativeArch (AndroidTargetArch arch)
	{
		WriteItem (NativeArchitectureLabel, arch.ToString ());
	}

	protected void AddNativeArchDesc (AndroidTargetArch arch)
	{
		AddLabeledItem (NativeArchitectureLabel, arch.ToString ());
	}

	protected void AddNativeArchDesc (ICollection<AndroidTargetArch> arches)
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
		foreach (AndroidTargetArch arch in arches) {
			architectures.Add (arch.ToString ());
		}

		AddLabeledItem (NativeArchitecturesLabel, String.Join (", ", architectures));
	}

	protected void WriteNativeArch (ICollection<AndroidTargetArch> arches)
	{
		if (arches.Count == 1) {
			WriteNativeArch (arches.First ());
			return;
		}

		WriteLabel (NativeArchitecturesLabel);
		if (arches.Count == 0) {
			WriteLine (InvalidValueColor, "none");
			return;
		}

		var architectures = new List<string> ();
		foreach (AndroidTargetArch arch in arches) {
			architectures.Add (arch.ToString ());
		}

		WriteLine (ValidValueColor, String.Join (", ", architectures));
	}

	protected MarkdownDocument AddYesNo (string label, bool value, bool appendNewLine = true) => AddLabeledItem (label, YesNo (value), appendNewLine);

	protected void WriteYesNo (string label, bool value) => WriteItem (label, YesNo (value));

	protected void WriteLabel (string label)
	{
		Write (LabelColor, $"{label}: ");
	}

	protected MarkdownDocument AddText (string text, MarkdownTextStyle style = MarkdownTextStyle.Plain)
	{
		return ReportDoc.AddText (text, style);
	}

	public MarkdownDocument AddListItemText (string text, MarkdownTextStyle style = MarkdownTextStyle.Plain)
	{
		return ReportDoc.AddText (text, style, addIndent: false);
	}

	protected void WriteItem (string label, string value)
	{
		WriteLabel (label);
		WriteLine (ValidValueColor, value);
	}

	protected void WriteLine ()
	{
		Console.WriteLine ();
	}

	protected void WriteLine (ConsoleColor color, string text)
	{
		Write (color, text);
		WriteLine ();
	}

	protected void Write (ConsoleColor color, string text)
	{
		ConsoleColor oldFG = Console.ForegroundColor;
		try {
			Console.ForegroundColor = color;
			Console.Write (text);
		} finally {
			Console.ForegroundColor = oldFG;
		}
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
