using System;

namespace Xamarin.Android.Tasks
{
	class NativeAssemblerStructContextDataProvider
	{
		/// <summary>
		/// Return size of a buffer <paramref name="fieldName"/> will point to, based on data passed in <paramref name="data"/>
		/// </summary>
		public virtual ulong GetBufferSize (object data, string fieldName)
		{
			return 0;
		}

		/// <summary>
		/// Return comment for the specified field, based on instance data passed in <paramref name="data"/>
		/// </summary>
		public virtual string GetComment (object data, string fieldName)
		{
			return String.Empty;
		}

		/// <summary>
		/// Get maximum width of data buffer allocated inline (that is as part of structure)
		/// </summary>
		public virtual uint GetMaxInlineWidth (object data, string fieldName)
		{
			return 0;
		}

		/// <summary>
		/// Returns name of the symbol the given field is supposed to point to. <c>null</c> or <c>String.Empty</c>
		/// can be returned to make the pointer <c>null</c>
		/// </summary>
		public virtual string? GetPointedToSymbolName (object data, string fieldName)
		{
			return null;
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
