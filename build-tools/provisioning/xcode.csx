if (IsMac) {
	const string MinMacOSVersion = "10.14.4";
	if (OSVersion < new Version (MinMacOSVersion))
		throw new Exception ($"macOS {MinMacOSVersion} or newer is required for Xcode 11.");
	if (OSVersion >= new Version (10.15.2))
		Item (XreItem.Xcode_11_6_0).XcodeSelect ();
	else
		Item (XreItem.Xcode_11_3_1).XcodeSelect ();
}
