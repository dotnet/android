using System;
using System.Collections;
using System.Collections.Generic;

namespace Java.Lang {
	partial class StringBuffer : IEnumerable, IEnumerable<char> {

#if JAVA_API_21
		IAppendable? IAppendable.Append (char c) =>
			Append (c);
		IAppendable? IAppendable.Append (ICharSequence? s) =>
			Append (s);
		IAppendable? IAppendable.Append (ICharSequence? s, int a, int b) =>
			Append (s, a, b);
#endif  // JAVA_API_21

	}
}
