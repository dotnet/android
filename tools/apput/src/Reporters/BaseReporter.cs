using System;

namespace ApplicationUtility;

abstract class BaseReporter : IReporter
{
	const ConsoleColor LabelColor = ConsoleColor.White;
	const ConsoleColor ValueColor = ConsoleColor.Green;

	public abstract void Report ();

	protected void WriteAspectDesc (string text)
	{
		WriteItem ("Aspect type", text);
	}

	protected void WriteNativeArch (NativeArchitecture arch)
	{
		WriteItem ("Native target architecture", arch.ToString ());
	}

	protected void WriteItem (string label, string value)
	{
		Write (LabelColor, $"{label}: ");
		WriteLine (ValueColor, value);
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
}
