namespace Microsoft.Android.Tasks;

internal static class TypeMapClassName
{
	public static bool TryGetClassName (string name, out string? className)
	{
		className = null;
		int dimensions = 0;
		while (dimensions < name.Length && name [dimensions] == '[') {
			dimensions++;
		}
		if (dimensions > 0) {
			if (dimensions > 255 || dimensions == name.Length) {
				return false;
			}
			// Primitive arrays are valid typemap entries but do not name a Java class to keep.
			if (dimensions == name.Length - 1 && "BCDFIJSZ".IndexOf (name [dimensions]) >= 0) {
				return true;
			}
			if (name [dimensions] != 'L' || name [name.Length - 1] != ';') {
				return false;
			}
			name = name.Substring (dimensions + 1, name.Length - dimensions - 2);
		}
		if (!GenerateTypeMapProguardConfiguration.IsClassName (name)) {
			return false;
		}
		className = name;
		return true;
	}
}
