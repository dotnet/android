package net.dot.android.test;

import java.util.ArrayList;
import java.util.Collection;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

final class InterfaceCollectionHolder {
	private final InterfaceCollectionBasePeer first;
	private final InterfaceCollectionBasePeer second;
	private final InterfaceCollectionPeer inheritedFirst;
	private final InterfaceCollectionPeer inheritedSecond;

	public InterfaceCollectionHolder() {
		first = new InterfaceCollectionBasePeer(11);
		second = new InterfaceCollectionBasePeer(22);
		inheritedFirst = new InterfaceCollectionPeer(33, 333);
		inheritedSecond = new InterfaceCollectionPeer(44, 444);
	}

	public List<ValueProvider> createList() {
		List<ValueProvider> result = new ArrayList<>();
		result.add(first);
		result.add(first);
		result.add(second);
		result.add(null);
		return result;
	}

	public List<ExtendedValueProvider> createInheritedList() {
		List<ExtendedValueProvider> result = new ArrayList<>();
		result.add(inheritedFirst);
		result.add(inheritedSecond);
		return result;
	}

	public Collection<ValueProvider> createCollection() {
		Collection<ValueProvider> result = new ArrayList<>();
		result.add(first);
		result.add(second);
		result.add(null);
		return result;
	}

	public Map<ValueProvider, String> createKeyDictionary() {
		Map<ValueProvider, String> result = new LinkedHashMap<>();
		result.put(first, "first");
		result.put(second, "second");
		result.put(null, "null");
		return result;
	}

	public Map<String, ValueProvider> createValueDictionary() {
		Map<String, ValueProvider> result = new LinkedHashMap<>();
		result.put("first", first);
		result.put("duplicate", first);
		result.put("second", second);
		result.put("null", null);
		return result;
	}

	public Map<ValueProvider, ValueProvider> createInterfaceDictionary() {
		Map<ValueProvider, ValueProvider> result = new LinkedHashMap<>();
		result.put(first, second);
		result.put(second, first);
		result.put(null, null);
		return result;
	}

	public List<ValueProvider> roundTripList(List<ValueProvider> value) {
		return value;
	}

	public Collection<ValueProvider> roundTripCollection(Collection<ValueProvider> value) {
		return value;
	}

	public Map<ValueProvider, String> roundTripKeyDictionary(Map<ValueProvider, String> value) {
		return value;
	}

	public Map<String, ValueProvider> roundTripValueDictionary(Map<String, ValueProvider> value) {
		return value;
	}

	public Map<ValueProvider, ValueProvider> roundTripInterfaceDictionary(Map<ValueProvider, ValueProvider> value) {
		return value;
	}
}
