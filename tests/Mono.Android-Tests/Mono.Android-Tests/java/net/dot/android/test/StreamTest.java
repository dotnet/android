package net.dot.android.test;

import android.util.Log;
import java.io.InputStream;
import java.io.IOException;

public class StreamTest
{
    static final String TAG = "StreamTest";
    public static final int BUFFER_SIZE = 1024;

    public static int InputStreamAdapter_Read(InputStream stream) throws IOException
    {
        Log.d(TAG, "StreamTest.InputStreamAdapter_Read, underlying stream type: " + stream.getClass().getName());
        return stream.read();
    }

    public static int InputStreamAdapter_Read_bytes(InputStream stream) throws IOException
    {
        Log.d(TAG, "StreamTest.InputStreamAdapter_Read_bytes, underlying stream type: " + stream.getClass().getName());
        byte[] buffer = new byte[BUFFER_SIZE];
        return stream.read(buffer);
    }
    public static int InputStreamAdapter_Read_bytes_int_int(InputStream stream) throws IOException
    {
        Log.d(TAG, "StreamTest.InputStreamAdapter_Read_bytes_int_int, underlying stream type: " + stream.getClass().getName());
        byte[] buffer = new byte[BUFFER_SIZE];
        return stream.read(buffer, 0, BUFFER_SIZE);
    }
}
