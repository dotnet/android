package com.jspecifyunmarked;

import org.jspecify.annotations.NonNull;
import org.jspecify.annotations.Nullable;

/**
 * Class in a package with no `package-info.class` and no class-level
 * `@NullMarked`. Only explicit annotations should produce nullness
 * output; the rest should be unknown (no attribute).
 */
public class JSpecifyUnmarked {

	public String defaultReturn (String value) {
		return value;
	}

	public @Nullable String nullableReturn (@Nullable String value) {
		return value;
	}

	public @NonNull String nonNullReturn (@NonNull String value) {
		return value;
	}

	public String defaultField;

	public @NonNull String nonNullField = "";
}
