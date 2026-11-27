using System;
using System.Globalization;

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
		const string prefix = "_ZTV";
		if (!symbol.StartsWith (prefix, StringComparison.Ordinal)) {
			return false;
		}
		int start = prefix.Length;
		int nameStart = start;
		while (nameStart < symbol.Length && symbol [nameStart] >= '0' && symbol [nameStart] <= '9') {
			nameStart++;
		}
		if (nameStart == start ||
				!uint.TryParse (symbol.Substring (start, nameStart - start), NumberStyles.None, CultureInfo.InvariantCulture, out uint length) ||
				length != symbol.Length - nameStart) {
			return false;
		}
		string name = RemoveDisambiguationSuffix (symbol.Substring (nameStart));
		// Match the emitted group identity, not the keys: JavaDictionary contains CLR names.
		const string sharedType = "_Java_Lang_Object";
		if (name.EndsWith (sharedType, StringComparison.Ordinal)) {
			return RemoveDisambiguationSuffix (name.Substring (0, name.Length - sharedType.Length)) == "Mono_Android";
		}
		const string localType = "___TypeMapAnchor";
		if (!name.EndsWith (localType, StringComparison.Ordinal)) {
			return false;
		}
		string assembly = RemoveDisambiguationSuffix (name.Substring (0, name.Length - localType.Length));
		return assembly.StartsWith ("_", StringComparison.Ordinal) && assembly.EndsWith ("_TypeMap", StringComparison.Ordinal);
	}

	// NativeAotNameMangler appends _<uint> when assembly or type names collide
	// after sanitization. Only that canonical suffix is part of the identity.
	static string RemoveDisambiguationSuffix (string name)
	{
		int separator = name.LastIndexOf ('_');
		if (separator < 0 || separator == name.Length - 1 ||
				(name [separator + 1] == '0' && separator + 2 != name.Length) ||
				!uint.TryParse (name.Substring (separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out _)) {
			return name;
		}
		return name.Substring (0, separator);
	}
}
