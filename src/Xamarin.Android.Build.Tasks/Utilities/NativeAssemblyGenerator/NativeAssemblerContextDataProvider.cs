using System;

namespace Xamarin.Android.Tasks
{
	class NativeAssemblerStructContextDataProvider
	{
		public virtual string GetComment (object data, string fieldName)
		{
			return String.Empty;
		}

		public virtual uint GetMaxInlineWidth (object data, string fieldName)
		{
			return 0;
		}
	}
}
