if (IsMac) {
	const string MinMacOSVersion = "10.14.4";
	const string MinMacOSVersionForLatestXcode = "10.15.4";
	if (OSVersion < new Version (MinMacOSVersion))
		throw new Exception ($"macOS {MinMacOSVersion} or newer is required for Xcode 11.");
	if (OSVersion >= new Version (MinMacOSVersionForLatestXcode))
		Xcode ("12.4").XcodeSelect ();
	else
		Xcode ("11.3.1").XcodeSelect ();
}
