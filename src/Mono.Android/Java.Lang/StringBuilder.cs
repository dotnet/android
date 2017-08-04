#if ANDROID_26

// It is introduced since API 26 not because of new member in StringBuffer
// but in AbstractStringBuilder which is nonpublic and affects the generated API.

using System;

namespace Java.Lang
{
	public partial class StringBuilder
	{
		public IAppendable Append (string s, int start, int end)
		{
			return Append (new Java.Lang.String (s), start, end);
		}
	}
}

#endif
