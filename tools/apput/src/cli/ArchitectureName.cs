using System;
using System.Collections.Generic;
using System.Linq;

namespace ApplicationUtility;

/// <summary>
/// Maps textual architecture names (e.g. "arm64-v8a", "x86_64") to <see cref="NativeArchitecture"/> values
/// and provides helpers for parsing user-supplied architecture lists.
/// </summary>
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

	/// <summary>
	/// Returns a dictionary mapping each known <see cref="NativeArchitecture"/> value to a comma-separated
	/// string of all recognized name aliases for that architecture.
	/// </summary>
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

	/// <summary>
	/// Parses a comma-separated string of architecture names into a set of <see cref="NativeArchitecture"/> values.
	/// The special value "all" includes every known architecture.
	/// </summary>
	/// <param name="v">A comma-separated list of architecture names, or "all".</param>
	/// <returns>A collection of parsed <see cref="NativeArchitecture"/> values.</returns>
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

			if (!NameMap.TryGetValue (a, out NativeArchitecture arch)) {
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
