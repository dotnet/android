
using System;
using Java.Interop;

namespace Java.Lang {

	public partial interface ICharSequence : IJavaPeerable
		, Android.Runtime.IJavaObject
	{
		char CharAt (int index);
		int Length ();
		Java.Lang.ICharSequence SubSequenceFormatted (int start, int end);
		string ToString ();
	}
}
