package net.dot.android;

import android.content.res.AssetManager;
import java.io.IOException;
import java.io.InputStream;

public final class JniRemappingAsset {
	private static AssetManager assetManager;

	private JniRemappingAsset () {}

	public static void initialize (AssetManager assets)
	{
		if (assets == null)
			throw new IllegalArgumentException ("The JNI remapping asset manager is missing.");
		assetManager = assets;
	}

	// Called by both native hosts before creating the managed JNI runtime.
	public static byte[] read (String runtimeIdentifier)
	{
		AssetManager assets = assetManager;
		if (assets == null)
			throw new IllegalStateException ("The JNI remapping asset manager has not been initialized.");

		String path = "xa-internal/jni-remap." + runtimeIdentifier + ".bin";
		try (InputStream stream = assets.open (path, AssetManager.ACCESS_BUFFER)) {
			int length = stream.available ();
			if (length <= 0)
				throw new IOException ("The JNI remapping asset is empty.");
			byte[] data = new byte[length];
			int offset = 0;
			while (offset < length) {
				int count = stream.read (data, offset, length - offset);
				if (count <= 0)
					throw new IOException ("The JNI remapping asset is truncated.");
				offset += count;
			}
			if (stream.read () != -1)
				throw new IOException ("The JNI remapping asset length changed while reading.");
			assetManager = null;
			return data;
		} catch (IOException e) {
			throw new IllegalStateException ("Could not read JNI remapping asset '" + path + "'.", e);
		}
	}
}
