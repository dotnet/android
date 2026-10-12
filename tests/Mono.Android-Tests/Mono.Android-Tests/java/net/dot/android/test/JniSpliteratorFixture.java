package net.dot.android.test;

import java.util.Objects;
import java.util.Spliterator;
import java.util.Spliterators;
import java.util.function.Consumer;

public final class JniSpliteratorFixture extends Spliterators.AbstractSpliterator<String> {
	private int position;

	public JniSpliteratorFixture() {
		super(2, Spliterator.ORDERED);
	}

	@Override
	public boolean tryAdvance(Consumer<? super String> action) {
		Objects.requireNonNull(action);
		if (position == 2) {
			return false;
		}
		action.accept(position++ == 0 ? "first" : "second");
		return true;
	}
}
