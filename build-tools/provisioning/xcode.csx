if (IsMac) {
	const string MinMacOSVersion = "10.15.4";
	const string MinMacOSVersionForLatestXcode = "11.3";
	if (OSVersion < new Version (MinMacOSVersion))
		throw new Exception ($"macOS {MinMacOSVersion} or newer is required for Xcode 12.");
	if (OSVersion >= new Version (MinMacOSVersionForLatestXcode))
		Xcode ("13.2").XcodeSelect ();
	else
		Xcode ("12.4").XcodeSelect ();
}
