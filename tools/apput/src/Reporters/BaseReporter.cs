using System;

namespace ApplicationUtility;

abstract class BaseReporter : IReporter
{
	protected const ConsoleColor LabelColor = ConsoleColor.White;
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

	protected void WriteAspectDesc (string text)
	{
		WriteItem ("Aspect type", text);
	}

	protected void WriteNativeArch (NativeArchitecture arch)
	{
		WriteItem ("Native target architecture", arch.ToString ());
	}

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
