using System;
using System.Collections.Generic;
using Java.Interop;

namespace Java.IO {

	public partial class PrintStream : Java.Lang.IAppendable {

		Java.Lang.IAppendable? Java.Lang.IAppendable.Append (char p0) => Append (p0);
		Java.Lang.IAppendable? Java.Lang.IAppendable.Append (Java.Lang.ICharSequence? p0) => Append (p0);
		Java.Lang.IAppendable? Java.Lang.IAppendable.Append (Java.Lang.ICharSequence? p0, int p1, int p2) => Append (p0, p1, p2);
	}
}
