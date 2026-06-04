using System;

namespace Xamarin.Installer.AndroidSDK.Common
{
	interface IAndroidArchive
	{
		string OS { get; }
		uint OSBits { get; }
		string Arch { get; }
		uint Size { get; }
		string Checksum { get; }
		string ChecksumType { get; }
		Uri Url { get; }
	}
}
