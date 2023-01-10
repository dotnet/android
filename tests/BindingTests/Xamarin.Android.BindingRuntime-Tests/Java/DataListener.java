package com.xamarin.android;

public interface DataListener {

	void onDataReceived (
			java.lang.String fromNode,
			java.lang.String fromChannel,
			java.lang.String payloadType,
			byte[][] payload
	);
}
