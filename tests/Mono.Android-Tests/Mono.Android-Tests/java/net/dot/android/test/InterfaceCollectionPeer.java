package net.dot.android.test;

final class InterfaceCollectionPeer implements ExtendedValueProvider {
	private final int value;
	private final int otherValue;

	public InterfaceCollectionPeer(int value, int otherValue) {
		this.value = value;
		this.otherValue = otherValue;
	}

	@Override
	public int getValue() {
		return value;
	}

	@Override
	public int getOtherValue() {
		return otherValue;
	}
}
