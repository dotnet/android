using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

class EnvironmentBuilder
{
	readonly Dictionary<string, string> environmentVariables;
	readonly Dictionary<string, string> systemProperties;
	readonly bool escapeValues;

	public IDictionary<string, string> EnvironmentVariables => environmentVariables;
	public IDictionary<string, string> SystemProperties => systemProperties;

	public EnvironmentBuilder (bool escapeValues = true)
	{
		environmentVariables = new Dictionary<string, string> (StringComparer.Ordinal);
		systemProperties = new Dictionary<string, string> (StringComparer.Ordinal);
		this.escapeValues = escapeValues;
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
		if (name.Length == 0) {
			throw new ArgumentException ("Environment variable name must not be empty", nameof (name));
		}
		if (!escapeValues && (name.IndexOf ('\0') >= 0 || value.IndexOf ('\0') >= 0)) {
			throw new ArgumentException ("Environment variables and system properties must not contain NUL characters");
		}

		string key = escapeValues ? ValidAssemblerString (name) : name;
		string contents = escapeValues ? ValidAssemblerString (value) : value;
		if (Char.IsUpper(name [0]) || !Char.IsLetter(name [0])) {
			environmentVariables [key] = contents;
		} else {
			systemProperties [key] = contents;
		}
	}

	public void AddEnvironmentVariableLine (string l)
	{
		string line = l.Trim ();
		if (line.IsNullOrEmpty () || line [0] == '#') {
			return;
		}

		string[] nv = line.Split (new char[]{'='}, 2);
		AddEnvironmentVariable (nv[0].Trim (), nv.Length < 2 ? "" : nv[1].Trim ());
	}

	static string ValidAssemblerString (string s) => s.Replace ("\\", "\\\\").Replace ("\"", "\\\"");
}
