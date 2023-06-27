namespace Java.Nio
{
#if NET || !ANDROID_34
	public partial class CharBuffer
	{
		// FIXME: these are generator limitation workaround: it should resolve
		// to generate explicit interface implementation for these methods.
		Java.Lang.IAppendable Java.Lang.IAppendable.Append(char ch)
		{
			return Append (ch)!;
		}

		Java.Lang.IAppendable Java.Lang.IAppendable.Append(Java.Lang.ICharSequence? csq)
		{
			return Append (csq)!;
		}

		Java.Lang.IAppendable Java.Lang.IAppendable.Append(Java.Lang.ICharSequence? csq, int start, int end)
		{
			return Append (csq, start, end)!;
		}
	}
#endif
}

