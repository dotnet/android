using System;

namespace Xamarin.Android.Tasks
{
	[Flags]
	enum SectionFlags
	{
		None                   = 0,
		Allocatable            = 1 << 0,
		GnuMbind               = 1 << 1,
		Excluded               = 1 << 2,
		ReferencesOtherSection = 1 << 3,
		Writable               = 1 << 4,
		Executable             = 1 << 5,
		Mergeable              = 1 << 6,
		HasCStrings            = 1 << 7,
		GroupMember            = 1 << 8,
		ThreadLocalStorage     = 1 << 9,
		MemberOfPreviousGroup  = 1 << 10,
		Retained               = 1 << 11,
		Number                 = 1 << 12,
		Custom                 = 1 << 13,
	}
}
