namespace Xamarin.Android.Tasks
{
	// This class may seem weird, but it's designed with the specific needs of AssemblyBlob instances in mind and also prepared for thread-safe use in the future, should the
	// need arise
	sealed class AssemblyBlobGlobalIndex
	{
		uint value = 0;

		public uint Value => value;

		/// <summary>
		///  Increments the counter and returns its <b>previous</b> value
		/// </summary>
		public uint Increment ()
		{
			uint ret = value++;
			return ret;
		}

		public void Subtract (uint count)
		{
			if (value < count) {
				return;
			}

			value -= count;
		}
	}
}
