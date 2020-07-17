if (IsMac) {
	const string MinMacOSVersion = "10.14.4";
	const string MinMacOSVersionForLatestXcode = "10.15.2";
	if (OSVersion < new Version (MinMacOSVersion))
		throw new Exception ($"macOS {MinMacOSVersion} or newer is required for Xcode 11.");
	if (OSVersion >= new Version (MinMacOSVersionForLatestXcode))
		Item (XreItem.Xcode_11_6_0).XcodeSelect ();
	else
		Item (XreItem.Xcode_11_3_1).XcodeSelect ();
}
