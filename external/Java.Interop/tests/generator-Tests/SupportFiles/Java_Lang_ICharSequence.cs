
using System;
using Java.Interop;

namespace Java.Lang {

	public partial interface ICharSequence : IJavaPeerable
#if !JAVA_INTEROP1
		, Android.Runtime.IJavaObject
#endif  // !JAVA_INTEROP1
	{
		char CharAt (int index);
		int Length ();
		Java.Lang.ICharSequence SubSequenceFormatted (int start, int end);
		string ToString ();
	}
}
