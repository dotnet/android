package net.dot.android.test;

public class InterfaceMarshalling {
	public static ValueProvider getExtendedValueProviderAsValueProvider() {
		return new ExtendedValueProvider() {
			@Override
			public int getValue() {
				return 42;
			}

			@Override
			public int getOtherValue() {
				return 84;
			}
		};
	}
}
