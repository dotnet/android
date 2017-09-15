using System;
using System.Collections.Generic;

namespace Android.Runtime {

	public static class CharSequence
	{
		public static Java.Lang.ICharSequence [] ArrayFromStringArray (string [] val)
		{
			throw new NotImplementedException ();
		}

		public static string [] ArrayToStringArray (Java.Lang.ICharSequence [] val)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr ToLocalJniHandle (string value)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr ToLocalJniHandle (Java.Lang.ICharSequence value)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr ToLocalJniHandle (IEnumerable<char> value)
		{
			throw new NotImplementedException ();
		}
	}
}
