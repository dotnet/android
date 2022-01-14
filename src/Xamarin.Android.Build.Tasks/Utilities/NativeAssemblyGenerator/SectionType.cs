namespace Xamarin.Android.Tasks
{
	enum SectionType
	{
		None,
		Data, // @progbits
		NoData, // @nobits
		InitArray, // @init_array
		FiniArray, // @fini_array
		PreInitArray, // @preinit_array
		Number, // @<number>
		Custom, // @<target specific>
	}
}
