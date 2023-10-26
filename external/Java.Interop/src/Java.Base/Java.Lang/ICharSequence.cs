using System.Collections;

namespace Java.Lang {

	partial class ICharSequenceInvoker : IEnumerable {
	}

	public static partial class ICharSequenceExtensions {

		public static ICharSequence[]? ToCharSequenceArray (this string?[]? values)
		{
			if (values == null) {
				return null;
			}
			var array = new ICharSequence [values.Length];
			for (int i = 0; i < values.Length; ++i) {
				if (values [i] == null) {
					continue;
				}
				array [i] = new Java.Lang.String (values [i]);
			}
			return array;
		}
	}
}
