package org.jspecify.annotations;

import java.lang.annotation.*;

@Target({
    ElementType.MODULE,
    ElementType.PACKAGE,
    ElementType.TYPE,
    ElementType.METHOD,
    ElementType.CONSTRUCTOR,
    ElementType.FIELD,
})
@Retention(RetentionPolicy.RUNTIME)
public @interface NullUnmarked {
}
