package com.xamarin.android;

public class Bxc7634 {

	public boolean finallyBlockRun;

	public void runFinallyBlock (Runnable r) {
		System.out.println ("Bxc7634.runFinallyBlock: start");
		try {
			r.run ();
			throw new Error ("Should not be reached");
		} finally {
			System.out.println ("Bxc7634.runFinallyBlock: executing finally!");
			finallyBlockRun = true;
		}
	}

	public Throwable throwableCaught;

	public void runCatchBlock (Runnable r) {
		System.out.println ("Bxc7634.runCatchBlock: start");
		try {
			r.run ();
			throw new Error ("Should not be reached");
		} catch (Throwable t) {
			System.out.println ("Bxc7634.runCatchBlock: caught exception! " + t);
			throwableCaught = t;
		}
	}
}
