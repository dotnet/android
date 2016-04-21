namespace Android.Views {

	partial class KeyCharacterMap {

		public int Get (Keycode keyCode, int metaState)
		{
			return Get (keyCode, (MetaKeyStates) metaState);
		}

		public char GetMatch (Keycode keyCode, char[] chars, int metaState)
		{
			return GetMatch (keyCode, chars, (MetaKeyStates) metaState);
		}
	}
}