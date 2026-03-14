using System;
using System.Collections.Generic;
using System.Linq;

namespace ApplicationUtility;

class ArchitectureName
{
	static readonly Dictionary<string, NativeArchitecture> NameMap = new (StringComparer.OrdinalIgnoreCase) {
		{ "arm64",       NativeArchitecture.Arm64 },
		{ "arm64-v8a",   NativeArchitecture.Arm64 },

		{ "arm",         NativeArchitecture.Arm },
		{ "arm32",       NativeArchitecture.Arm },
		{ "armeabi-v7a", NativeArchitecture.Arm },

		{ "x86",         NativeArchitecture.X86 },
		{ "x86-32",      NativeArchitecture.X86 },
		{ "i686",        NativeArchitecture.X86 },
		{ "i386",        NativeArchitecture.X86 },

		{ "x86_64",      NativeArchitecture.X64 },
		{ "x86-64",      NativeArchitecture.X64 },
		{ "x64",         NativeArchitecture.X64 },
		{ "amd64",       NativeArchitecture.X64 },
	};

	public static IDictionary<NativeArchitecture, string> GetSupportedNames ()
	{
		var ret = new Dictionary<NativeArchitecture, string> ();

		foreach (NativeArchitecture arch in Enum.GetValues<NativeArchitecture> ()) {
			if (arch == NativeArchitecture.Unknown) {
				continue;
			}

			ret.Add (
				arch,
				String.Join (", ", NameMap.Where (kvp => kvp.Value == arch).Select (kvp => kvp.Key))
			);
		}

		return ret;
	}

	public static ICollection<NativeArchitecture> ParseList (string v)
	{
		var ret = new HashSet<NativeArchitecture> ();
		if (String.IsNullOrEmpty (v)) {
			return ret;
		}

		// Short-circuit the simple case
		if (IsAll (v)) {
			AddAll ();
			return ret;
		}

		foreach (string a in v.Split (',', StringSplitOptions.RemoveEmptyEntries)) {
			if (IsAll (v)) {
				AddAll ();
				break;
			}

			if (NameMap.TryGetValue (a, out NativeArchitecture arch)) {
				Log.Warning ($"Unrecognized architecture name '{a}', ignoring.");
				continue;
			}
			ret.Add (arch);
		}

		return ret;

		bool IsAll (string s) => String.Equals (s, "all", StringComparison.OrdinalIgnoreCase);

		void AddAll ()
		{
			ret.Clear ();
			foreach (NativeArchitecture arch in Enum.GetValues<NativeArchitecture> ()) {
				if (arch == NativeArchitecture.Unknown) {
					continue;
				}

				ret.Add (arch);
			}
		}
	}
}
