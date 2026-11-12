package com.xamarin;

import android.annotation.NonNull;

public class NotNullClass {

	public void nullFunc (String value) {
	}

	@NonNull
	public void notNullFunc (@NonNull String value) {
	}

	public String nullField;

	@NonNull
	public String notNullField;
}
