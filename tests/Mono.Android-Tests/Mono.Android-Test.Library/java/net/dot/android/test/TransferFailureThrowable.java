package net.dot.android.test;

public class TransferFailureThrowable extends RuntimeException {
	private final int stage;
	private int stackCalls;

	public TransferFailureThrowable(int stage) {
		this.stage = stage;
	}

	public void reset() {
		stackCalls = 0;
	}

	@Override
	public String getMessage() {
		if (stage == 1)
			throw new IllegalStateException("transfer-extraction-1");
		return "transfer test";
	}

	@Override
	public synchronized Throwable getCause() {
		if (stage == 2)
			throw new IllegalStateException("transfer-extraction-2");
		return null;
	}

	@Override
	public void printStackTrace(java.io.PrintWriter writer) {
		stackCalls++;
		if (stage == 3 || (stage == 4 && stackCalls == 2))
			throw new IllegalStateException("transfer-extraction-" + stage);
		writer.print("transfer test stack");
	}
}
