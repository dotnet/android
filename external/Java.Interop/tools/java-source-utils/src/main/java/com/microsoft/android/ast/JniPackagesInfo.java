package com.microsoft.android.ast;

import java.util.Collection;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.stream.Collectors;

import com.microsoft.android.util.Parameter;

public final class JniPackagesInfo {

	private final Map<String, JniPackageInfo> packages = new HashMap<String, JniPackageInfo>();

	public JniPackageInfo getPackage(String packageName) {
		packageName = Parameter.normalize(packageName, "");

		if (!packages.containsKey(packageName)) {
			JniPackageInfo newPackage = new JniPackageInfo(packageName);
			packages.put(packageName, newPackage);
			return newPackage;
		}
		return packages.get(packageName);
	}

	public final Collection<JniPackageInfo> getPackages() {
		return packages.values();
	}

	public final Collection<JniPackageInfo> getSortedPackages() {
		final List<JniPackageInfo> sortedPackages = packages.values()
			.stream()
			.sorted((p1, p2) -> p1.getPackageName().compareTo(p2.getPackageName()))
			.collect(Collectors.toList());
		return sortedPackages;
	}
}
