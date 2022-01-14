using System;

namespace Xamarin.Android.Tasks
{
	class NativeAssemblerStructContextDataProvider
	{
		public virtual string GetComment (object data, string fieldName)
		{
			return String.Empty;
		}

		// Get maximum width of data buffer allocated inline (that is not pointed to)
		public virtual uint GetMaxInlineWidth (object data, string fieldName)
		{
			return 0;
		}
	}
}
