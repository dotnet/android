using System;

namespace Android.Views {

	partial class KeyEvent {

		[Obsolete ("Please use GetMatch(char[], MetaKeyStates)")]
		public char GetMatch (char[] chars, int metaState)
		{
			return GetMatch (chars, (MetaKeyStates) metaState);
		}

		[Obsolete ("Please use GetUnicodeChar(MetaKeyStates)")]
		public int GetUnicodeChar (int meta)
		{
			return GetUnicodeChar ((MetaKeyStates) meta);
		}
	}
}