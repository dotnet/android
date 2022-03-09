using System;

namespace Xamarin.Android.Tasks
{
	class NativeAssemblerStructContextDataProvider
	{
		public virtual ulong GetBufferSize (object data, string fieldName)
		{
			return 0;
		}

		public virtual string GetComment (object data, string fieldName)
		{
			return String.Empty;
		}

		// Get maximum width of data buffer allocated inline (that is not pointed to)
		public virtual uint GetMaxInlineWidth (object data, string fieldName)
		{
			return 0;
		}

		protected T EnsureType <T> (object data) where T: class
		{
			var ret = data as T;
			if (ret == null) {
				throw new InvalidOperationException ($"Invalid data type, expected an instance of {typeof(T)}");
			}

			return ret;
		}
	}
}
