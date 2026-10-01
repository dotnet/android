package net.dot.android.test;

public class VirtualCallbackConstructorBase {
	public VirtualCallbackConstructorBase (int value) {
		onConstructed (value);
	}

	public void onConstructed (int value) {
	}
}
