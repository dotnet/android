package androidx.annotation;

import java.lang.annotation.ElementType;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;

/**
 * Test-only stub of the declaration-target `androidx.annotation.Nullable`.
 * Unlike `org.jspecify.annotations.Nullable`, this is *not* `TYPE_USE`, so
 * it lands in `Runtime(In)visibleAnnotations` and exercises the declaration-
 * level nullable resolution path.
 */
@Retention(RetentionPolicy.CLASS)
@Target({ ElementType.FIELD, ElementType.METHOD, ElementType.PARAMETER, ElementType.LOCAL_VARIABLE })
public @interface Nullable {
}
