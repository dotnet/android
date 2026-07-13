using System;
using System.Text.RegularExpressions;
using System.IO;

namespace Xamarin.Android.Tools
{
	[Flags]
	public enum AndroidTargetArch
	{
		None = 0,
		Arm = 1,
		X86 = 2,
		Mips = 4,
		Arm64 = 8,
		X86_64 = 16,
		Other = 0x10000 // hope it's not too optimistic
	}
}

