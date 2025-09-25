using System;
using System.Collections.Generic;
using System.Linq;

using Xamarin.Android.Tools;

namespace ApplicationUtility;

abstract class BaseReporter : IReporter
{
	const string NativeArchitectureLabel = "Native target architecture";
	const string NativeArchitecturesLabel = NativeArchitectureLabel + "s";

	protected const ConsoleColor LabelColor = ConsoleColor.Gray;
	protected const ConsoleColor ValidValueColor = ConsoleColor.Green;
	protected const ConsoleColor InvalidValueColor = ConsoleColor.Red;
	protected const ConsoleColor BannerColor = ConsoleColor.Cyan;

	protected abstract string AspectName { get; }
	protected abstract string ShortDescription { get; }

	public void Report ()
	{
		WriteLine (BannerColor, $"# {AspectName} ({ShortDescription})");
		DoReport ();
		WriteLine ();
	}

	protected abstract void DoReport ();

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

	protected void WriteYesNo (string label, bool value) => WriteItem (label, YesNo (value));

	protected void WriteLabel (string label)
	{
		Write (LabelColor, $"{label}: ");
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

	protected string YesNo (bool yes) => yes ? "yes" : "no";
	protected string ValueOrNone (string? s) => String.IsNullOrEmpty (s) ? "none" : s;
}
