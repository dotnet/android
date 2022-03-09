namespace Xamarin.Android.Tasks.LLVMIR
{
	sealed class StructureStringData
	{
		public string? VariableName { get; }
		public ulong Size { get; }

		public StructureStringData (string? name, ulong size)
		{
			VariableName = name;
			Size = size;
		}
	}
}
