package net.dot.jni.test;

public class TransferFailureThrowable extends RuntimeException {
	private final int stage;

	public TransferFailureThrowable(int stage) {
		this.stage = stage;
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
		if (stage == 3)
			throw new IllegalStateException("transfer-extraction-3");
		writer.print("transfer test stack");
	}
}
