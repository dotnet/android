namespace Xamarin.Android.Tasks
{
	class CompressedAssemblyInfo
	{
		public const string CompressedAssembliesInfoKey = "__CompressedAssembliesInfo";

		public uint FileSize { get; }
		public uint DescriptorIndex { get; set; }

		public CompressedAssemblyInfo (uint fileSize)
		{
			FileSize = fileSize;
			DescriptorIndex = 0;
		}
	}
}
