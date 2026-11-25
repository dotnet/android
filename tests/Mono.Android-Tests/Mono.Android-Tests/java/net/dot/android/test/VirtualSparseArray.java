package net.dot.android.test;

import android.util.SparseArray;

public class VirtualSparseArray extends SparseArray<Object> {
	protected String lastCall = "";

	public String getLastCall() {
		return lastCall;
	}

	@Override
	public void append(int key, Object value) {
		lastCall = "append:" + key + ":" + value;
	}

	@Override
	public Object get(int key) {
		lastCall = "get:" + key;
		return lastCall;
	}

	@Override
	public Object get(int key, Object value) {
		lastCall = "getDefault:" + key + ":" + value;
		return lastCall;
	}

	@Override
	public int indexOfValue(Object value) {
		lastCall = "indexOfValue:" + value;
		return 123;
	}

	@Override
	public void put(int key, Object value) {
		lastCall = "put:" + key + ":" + value;
	}

	@Override
	public void setValueAt(int index, Object value) {
		lastCall = "setValueAt:" + index + ":" + value;
	}

	@Override
	public Object valueAt(int index) {
		lastCall = "valueAt:" + index;
		return lastCall;
	}

	public static final class Derived extends VirtualSparseArray {
		@Override
		public void put(int key, Object value) {
			lastCall = "derivedPut:" + key + ":" + value;
		}
	}
}
