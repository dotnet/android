package net.dot.android.test;

import java.util.ArrayList;
import java.util.Collection;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

final class ValueTypeContainerFixture {
	public ValueTypeContainerFixture() {
	}

	public Object[] createArray(int length) {
		return new Object[length];
	}

	public Object[] roundTripArray(Object[] value) {
		return value;
	}

	public List<Object> createList() {
		return new ArrayList<>();
	}

	public List<Object> roundTripList(List<Object> value) {
		return value;
	}

	public Collection<Object> createCollection() {
		return new ArrayList<>();
	}

	public Collection<Object> roundTripCollection(Collection<Object> value) {
		return value;
	}

	public Map<Object, Object> createDictionary() {
		return new LinkedHashMap<>();
	}

	public Map<Object, Object> roundTripDictionary(Map<Object, Object> value) {
		return value;
	}
}
