package com.xamarin.android;

public class Bxc37706Throwable extends java.lang.Throwable {
	public String getMessage() {
		getMessageInvoked = true;
		return super.getMessage();
	}
	
	public boolean getMessageInvoked;
}