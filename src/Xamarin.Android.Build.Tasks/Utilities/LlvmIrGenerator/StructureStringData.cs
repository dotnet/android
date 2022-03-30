namespace Xamarin.Android.Tasks.LLVMIR
{
	sealed class StructurePointerData
	{
		public string? VariableName { get; }
		public ulong Size { get; }

		public StructurePointerData (string? name, ulong size)
		{
			VariableName = name;
			Size = size;
		}
	}
}
