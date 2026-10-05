using System;

namespace Microsoft.Android.Tasks;

static class TypeMapKey
{
	public static string NormalizeAliasKey (string key)
	{
		int start = key.LastIndexOf ('[');
		if (start <= 0 || start == key.Length - 2 || key [key.Length - 1] != ']') {
			return key;
		}
		for (int i = start + 1; i < key.Length - 1; i++) {
			if (key [i] < '0' || key [i] > '9') {
				return key;
			}
		}
		return key.Substring (0, start);
	}

	public static bool IsJavaGroupSymbol (string symbol)
	{
		int marker = symbol.IndexOf ("_ZTV", StringComparison.Ordinal);
		if (marker < 0) {
			return false;
		}
		int start = marker + 4;
		int nameStart = start;
		while (nameStart < symbol.Length && symbol [nameStart] >= '0' && symbol [nameStart] <= '9') {
			nameStart++;
		}
		if (nameStart == start) {
			return false;
		}
		string name = symbol.Substring (nameStart);
		// Match the emitted group identity, not the keys: JavaDictionary contains CLR names.
		return name == "Mono_Android_Java_Lang_Object" ||
			(name.StartsWith ("_", StringComparison.Ordinal) && name.EndsWith ("_TypeMap___TypeMapAnchor", StringComparison.Ordinal));
	}
}
