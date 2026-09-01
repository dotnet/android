package net.dot.android.test;

final class InterfaceCollectionBasePeer implements ValueProvider {
	private final int value;

	public InterfaceCollectionBasePeer(int value) {
		this.value = value;
	}

	@Override
	public int getValue() {
		return value;
	}
}
