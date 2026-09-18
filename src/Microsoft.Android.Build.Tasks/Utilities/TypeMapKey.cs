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
}
