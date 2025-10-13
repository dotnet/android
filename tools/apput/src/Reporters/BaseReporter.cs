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

	protected MarkdownHeading AddSection (string text)
	{
		if (!ReportDoc.IsEmpty) {
			ReportDoc.AddNewLine ();
		}
		return ReportDoc.AddHeading (level: 1, text);
	}

	protected MarkdownElement CreateItemWithLabel (string label, string text, bool endWithNewline = true)
	{
		var item = new MarkdownTextSpan ($"{label}:") {
			Bold = true
		};
		item.AddText ($" {text}");
		if (endWithNewline) {
			item.AddNewline ();
		}

		return item;
	}

	protected void AddAspectDesc (string text)
	{
		MarkdownHeading section = ReportDoc.AddHeading (1, "Generic aspect information");
		var para = MarkdownDocument.CreateParagraph ();
		para.AddChild (CreateItemWithLabel ("Aspect type", text, endWithNewline: false));
		section.AddChild (para);
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

	protected void AddNativeArchDesc (MarkdownContainerElement container, AndroidTargetArch arch)
	{
		container.AddChild (CreateItemWithLabel (NativeArchitectureLabel, arch.ToString ()));
	}

	protected void AddNativeArchDesc (MarkdownContainerElement container, ICollection<AndroidTargetArch> arches)
	{
		if (arches.Count == 1) {
			AddNativeArchDesc (container, arches.First ());
			return;
		}

		if (arches.Count == 0) {
			container.AddChild (MarkdownDocument.CreateText ("none", bold: true));
			return;
		}

		var architectures = new List<string> ();
		foreach (AndroidTargetArch arch in arches) {
			architectures.Add (arch.ToString ());
		}

		container.AddChild (CreateItemWithLabel (NativeArchitecturesLabel, String.Join (", ", architectures)));
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

	protected void AddYesNo (MarkdownContainerElement container, string label, bool value)
	{
		container.AddChild (CreateItemWithLabel (label, YesNo (value)));
	}

	protected void WriteYesNo (string label, bool value) => WriteItem (label, YesNo (value));

	protected void WriteLabel (string label)
	{
		Write (LabelColor, $"{label}: ");
	}

	protected void AddText (MarkdownContainerElement container, string text, bool bold = false)
	{
		container.AddChild (MarkdownDocument.CreateText (text, bold: bold));
	}

	protected void AddItem (MarkdownContainerElement container, string label, string value)
	{
		container.AddChild (CreateItemWithLabel (label, value));
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
