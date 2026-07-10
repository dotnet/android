package com.jspecifytest;

import org.jspecify.annotations.NullMarked;
import org.jspecify.annotations.Nullable;

/**
 * Class-level `@NullMarked` inside an already-marked package, with a
 * `@Nullable` opt-out, exercises the class-level scope code path.
 */
@NullMarked
public class JSpecifyClassMarked {

	public String defaultReturn (String value) {
		return value;
	}

	public @Nullable String nullableReturn (@Nullable String value) {
		return value;
	}

	public String defaultField;
	public @Nullable String nullableField;
}
