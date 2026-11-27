package com.xamarin;

import java.util.List;
import java.util.ArrayList;

interface IJavaInterface<TString extends CharSequence & Appendable, TStringList extends ArrayList<TString> & List<TString>, TReturn> extends Runnable {

	@Deprecated
	public static final int	STATIC_FINAL_INT    = 1;

	TReturn func (TString value);
}