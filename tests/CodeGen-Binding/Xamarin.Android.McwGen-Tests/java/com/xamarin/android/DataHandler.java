package com.xamarin.android;

public class DataHandler {
	DataListener listener;

	public void setOnDataListener (DataListener listener)
	{
		this.listener = listener;
	}

	public void send ()
	{
		if (listener == null)
			return;
		byte start = (byte) 'J';
		byte[][] data = new byte[][]{
			new byte[]{ (byte) (start + 11), (byte) (start + 12), (byte) (start + 13) },
			new byte[]{ (byte) (start + 21), (byte) (start + 22), (byte) (start + 23) },
			new byte[]{ (byte) (start + 31), (byte) (start + 32), (byte) (start + 33) },
		};
		listener.onDataReceived ("fromNode", "fromChannel", "payloadType", data);
		for (int i = 0; i < data.length; ++i)
			for (int j = 0; j < data [i].length; ++j) {
				int expected = ((i+1)*10) + (j+1);
				if (data [i][j] != ((i+1)*10) + (j+1))
					throw new Error ("Value mismatch! Should be '" + expected +
							"'; was '" + data [i][j] + "'.");
			}
	}
}
