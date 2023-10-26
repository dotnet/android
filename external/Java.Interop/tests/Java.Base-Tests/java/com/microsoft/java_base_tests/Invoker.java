package com.microsoft.java_base_tests;

import java.util.function.IntConsumer;

public final class Invoker {

	public static void run(Runnable r) {
		r.run();
	}

	public static Runnable createRunnable(final IntConsumer consumer) {
		return new Runnable() {
			int value;
			public void run() {
				consumer.accept(value++);
			}
		};
	}
}
