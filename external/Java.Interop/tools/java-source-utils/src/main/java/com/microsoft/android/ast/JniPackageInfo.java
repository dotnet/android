package com.microsoft.android.ast;

import java.util.Collection;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.stream.Collectors;

import com.microsoft.android.util.Parameter;

public final class JniPackageInfo {
	private final String packageName;

	private final Map<String, JniTypeInfo> types = new HashMap<String, JniTypeInfo>();

	public JniPackageInfo(String packageName) {
		packageName = Parameter.normalize(packageName, "");

		this.packageName = packageName;
	}

	public final String getPackageName() {
		return this.packageName;
	}

	public final JniTypeInfo getType(String typeName) {
		return types.getOrDefault(typeName, null);
	}

	public final void add(JniTypeInfo type) {
		if (types.containsKey(type.getName()))
			throw new IllegalArgumentException("type");
		types.put(type.getName(), type);
	}

	public final Collection<JniTypeInfo> getTypes() {
		return types.values();
	}

	public final Collection<JniTypeInfo> getSortedTypes() {
		final List<JniTypeInfo> sortedTypes = types.values()
			.stream()
			.sorted((t1, t2) -> t1.getRawName().compareTo(t2.getRawName()))
			.collect(Collectors.toList());
		return sortedTypes;
	}
}
