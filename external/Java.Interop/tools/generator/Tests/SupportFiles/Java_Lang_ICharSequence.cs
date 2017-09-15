using System;
using Android.Runtime;

namespace Java.Lang {

	public partial interface ICharSequence : IJavaObject
	{
		char CharAt (int index);
		int Length ();
		Java.Lang.ICharSequence SubSequenceFormatted (int start, int end);
		string ToString ();
	}
}
