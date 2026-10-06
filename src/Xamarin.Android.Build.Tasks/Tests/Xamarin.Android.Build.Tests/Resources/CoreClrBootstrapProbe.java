package net.dot.android;

import java.util.Arrays;
import java.util.Base64;

public final class CoreClrBootstrapProbe
{
	private static native byte[] snapshot (boolean jniFlags);

	public static void main (String[] args)
	{
		String scenario = args [1];
		switch (scenario) {
			case "negative-count": AppBootstrapConfig.NativeConfigLayout [0] = -1; break;
			case "invalid-offset": AppBootstrapConfig.NativeConfigLayout [4] = AppBootstrapConfig.NativeConfig.length; break;
			case "missing-terminator": AppBootstrapConfig.NativeConfig [AppBootstrapConfig.NativeConfig.length - 1] = 1; break;
			case "invalid-flag": AppBootstrapConfig.NativeLibraryFlags [0] = 4; break;
			case "preload-without-jni": AppBootstrapConfig.NativeLibraryFlags [0] = 2; break;
			case "jni-flags":
				AppBootstrapConfig.NativeLibraryFlags [0] = 3;
				AppBootstrapConfig.NativeLibraryFlags [1] = 1;
				break;
		}
		System.load (args [0]);
		Arrays.fill (AppBootstrapConfig.NativeConfig, (byte)'!');
		Arrays.fill (AppBootstrapConfig.NativeConfigLayout, -1);
		Arrays.fill (AppBootstrapConfig.NativeLibraryFlags, (byte)4);
		System.out.print (Base64.getEncoder ().encodeToString (snapshot (scenario.equals ("jni-flags"))));
	}
}
