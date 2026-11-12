using System;

namespace MonoDroid.Generation {

	public class SimpleSymbol : FormatSymbol {

		public SimpleSymbol (string default_value, string java_type, string type, string jni_type, string native_type = null, string from_fmt="{0}", string to_fmt="{0}", string returnCast = null)
			: base (default_value, java_type, jni_type, native_type ?? type, type, from_fmt, to_fmt, returnCast)
		{
		}

	}
}

