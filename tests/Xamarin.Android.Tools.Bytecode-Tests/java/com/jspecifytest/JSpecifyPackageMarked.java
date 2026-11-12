package com.jspecifytest;

import java.util.List;

import androidx.annotation.Nullable;
import org.jspecify.annotations.NullUnmarked;

/**
 * Lives inside a `@NullMarked` package. Without any annotations,
 * all reference-typed return values, parameters, and fields should
 * be considered non-null.
 */
public class JSpecifyPackageMarked {

	// Reference return / param / field with no annotations -> non-null.
	public String defaultReturn (String value) {
		return value;
	}

	public String defaultField;

	// Primitive return / field — never gets a `not-null` attribute.
	public int primitiveReturn () {
		return 0;
	}

	public int primitiveField;

	// TYPE_USE `@Nullable` overrides scope default.
	public @org.jspecify.annotations.Nullable String nullableReturn (@org.jspecify.annotations.Nullable String value) {
		return value;
	}

	public @org.jspecify.annotations.Nullable String nullableField;

	// Declaration-style `@Nullable` (lives in `Runtime(In)visibleAnnotations`,
	// not the type-annotation table) must also override the scope default.
	@Nullable
	public String declarationNullableReturn (@Nullable String value) {
		return value;
	}

	@Nullable
	public String declarationNullableField;

	@NullUnmarked
	public String unmarkedReturn (String value) {
		return value;
	}

	// Type-variable usages have parametric nullness per JSpecify;
	// they must not gain `not-null` from the scope default even
	// though their erased descriptor is `Ljava/lang/Object;`.
	public <T> T typeVariableReturn (T value) {
		return value;
	}

	// Nested `@Nullable` on an inner type argument. The container
	// (List) is still non-null in a `@NullMarked` scope; the inner
	// annotation has a non-empty `type_path` and must be ignored
	// at the top level.
	public List<@org.jspecify.annotations.Nullable String> nestedNullableField;

	public class Inner {
		public class Deeper {
		}
	}

	public class GenericInner<T> {
	}

	public JSpecifyPackageMarked.@org.jspecify.annotations.Nullable Inner nullableMemberField;

	public JSpecifyPackageMarked.@org.jspecify.annotations.Nullable Inner nullableMemberReturn (JSpecifyPackageMarked.@org.jspecify.annotations.Nullable Inner value) {
		return value;
	}

	public JSpecifyPackageMarked.Inner.@org.jspecify.annotations.Nullable Deeper nullableDeepMemberField;

	public JSpecifyPackageMarked.Inner.@org.jspecify.annotations.Nullable Deeper nullableDeepMemberReturn (JSpecifyPackageMarked.Inner.@org.jspecify.annotations.Nullable Deeper value) {
		return value;
	}

	public List<JSpecifyPackageMarked.@org.jspecify.annotations.Nullable Inner> nullableMemberArgumentField;

	public List<JSpecifyPackageMarked.@org.jspecify.annotations.Nullable Inner> nullableMemberArgumentReturn (List<JSpecifyPackageMarked.@org.jspecify.annotations.Nullable Inner> value) {
		return value;
	}

	public JSpecifyPackageMarked.GenericInner<@org.jspecify.annotations.Nullable String> nullableMemberTypeArgumentField;

	public JSpecifyPackageMarked.GenericInner<@org.jspecify.annotations.Nullable String> nullableMemberTypeArgumentReturn (JSpecifyPackageMarked.GenericInner<@org.jspecify.annotations.Nullable String> value) {
		return value;
	}

	public String @org.jspecify.annotations.Nullable [] nullableArrayField;

	public String @org.jspecify.annotations.Nullable [] nullableArrayReturn (String @org.jspecify.annotations.Nullable [] value) {
		return value;
	}

	public @org.jspecify.annotations.Nullable String [] nullableArrayElementField;

	public @org.jspecify.annotations.Nullable String [] nullableArrayElementReturn (@org.jspecify.annotations.Nullable String [] value) {
		return value;
	}

	public JSpecifyPackageMarked.@org.jspecify.annotations.Nullable Inner [] nullableMemberArrayElementField;

	public JSpecifyPackageMarked.@org.jspecify.annotations.Nullable Inner [] nullableMemberArrayElementReturn (JSpecifyPackageMarked.@org.jspecify.annotations.Nullable Inner [] value) {
		return value;
	}

	@javax.annotation.CheckForNull
	public String javaxCheckForNullField;

	@javax.annotation.CheckForNull
	public String javaxCheckForNullReturn (@javax.annotation.CheckForNull String value) {
		return value;
	}

	@edu.umd.cs.findbugs.annotations.CheckForNull
	public String findbugsCheckForNullField;

	@edu.umd.cs.findbugs.annotations.CheckForNull
	public String findbugsCheckForNullReturn (@edu.umd.cs.findbugs.annotations.CheckForNull String value) {
		return value;
	}
}
