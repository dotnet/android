package net.dot.android.test;

import java.util.Objects;
import java.util.Spliterator;
import java.util.Spliterators;
import java.util.function.Consumer;

public final class HandwrittenSpliterator extends Spliterators.AbstractSpliterator<String> {
    private boolean advanced;

    public HandwrittenSpliterator() {
        super(1L, Spliterator.ORDERED);
    }

    @Override
    public boolean tryAdvance(Consumer<? super String> action) {
        Objects.requireNonNull(action);
        if (advanced) {
            return false;
        }
        advanced = true;
        action.accept("item");
        return true;
    }
}
