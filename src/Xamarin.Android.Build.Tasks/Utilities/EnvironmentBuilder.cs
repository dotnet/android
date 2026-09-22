using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

class EnvironmentBuilder
{
	readonly Dictionary<string, string> environmentVariables;
	readonly Dictionary<string, string> systemProperties;

	public IDictionary<string, string> EnvironmentVariables => environmentVariables;
	public IDictionary<string, string> SystemProperties => systemProperties;

	public EnvironmentBuilder ()
	{
		environmentVariables = new Dictionary<string, string> (StringComparer.Ordinal);
		systemProperties = new Dictionary<string, string> (StringComparer.Ordinal);
	}

	public void Read (ITaskItem[]? envItems)
	{
		foreach (ITaskItem env in envItems ?? []) {
			foreach (string line in File.ReadLines (env.ItemSpec)) {
				AddEnvironmentVariableLine (line);
			}
		}
	}

	public void AddEnvironmentVariable (string name, string value)
	{
		if (Char.IsUpper(name [0]) || !Char.IsLetter(name [0])) {
			environmentVariables [ValidAssemblerString (name)] = ValidAssemblerString (value);
		} else {
			systemProperties [ValidAssemblerString (name)] = ValidAssemblerString (value);
		}
	}

	public void AddEnvironmentVariableLine (string l)
	{
		string line = l.Trim ();
		if (line.IsNullOrEmpty () || line [0] == '#') {
			return;
		}

		string[] nv = line.Split (new char[]{'='}, 2);
		AddEnvironmentVariable (nv[0].Trim (), nv.Length < 2 ? String.Empty : nv[1].Trim ());
	}

	static string ValidAssemblerString (string s) => s.Replace ("\\", "\\\\").Replace ("\"", "\\\"");
}
