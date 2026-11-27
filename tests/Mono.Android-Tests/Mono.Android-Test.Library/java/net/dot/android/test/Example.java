package net.dot.android.test;

public class Example {
	public static ValueProvider getValueProvider() {
		return new ValueProvider() {
			@Override
			public int getValue() {
				return 42;
			}
		};
	}
}
