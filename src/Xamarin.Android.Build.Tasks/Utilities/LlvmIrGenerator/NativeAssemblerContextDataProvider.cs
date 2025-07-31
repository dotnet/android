using System;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Base class for providing context-specific data for native assembler structure generation.
	/// Allows dynamic data generation based on structure instances during LLVM IR generation.
	/// </summary>
	class NativeAssemblerStructContextDataProvider
	{
		/// <summary>
		/// Return size of a buffer <paramref name="fieldName"/> will point to, based on data passed in <paramref name="data"/>
		/// </summary>
		/// <param name="data">The structure instance data.</param>
		/// <param name="fieldName">The name of the field that needs buffer sizing.</param>
		/// <returns>The size of the buffer in bytes.</returns>
		public virtual ulong GetBufferSize (object data, string fieldName)
		{
			return 0;
		}

		/// <summary>
		/// Return comment for the specified field, based on instance data passed in <paramref name="data"/>
		/// </summary>
		/// <param name="data">The structure instance data.</param>
		/// <param name="fieldName">The name of the field that needs a comment.</param>
		/// <returns>The comment string for the field.</returns>
		public virtual string GetComment (object data, string fieldName)
		{
			return String.Empty;
		}

		/// <summary>
		/// Get maximum width of data buffer allocated inline (that is as part of structure)
		/// </summary>
		/// <param name="data">The structure instance data.</param>
		/// <param name="fieldName">The name of the field that needs inline width information.</param>
		/// <returns>The maximum inline width in bytes.</returns>
		public virtual uint GetMaxInlineWidth (object data, string fieldName)
		{
			return 0;
		}

		/// <summary>
		/// Returns name of the symbol the given field is supposed to point to. <c>null</c> or <c>String.Empty</c>
		/// can be returned to make the pointer <c>null</c>
		/// </summary>
		/// <param name="data">The structure instance data.</param>
		/// <param name="fieldName">The name of the field that needs symbol name information.</param>
		/// <returns>The symbol name the field should point to, or null for a null pointer.</returns>
		public virtual string? GetPointedToSymbolName (object data, string fieldName)
		{
			return null;
		}

		/// <summary>
		/// Ensures that the provided data object is of the expected type.
		/// </summary>
		/// <typeparam name="T">The expected type of the data object.</typeparam>
		/// <param name="data">The data object to validate.</param>
		/// <returns>The data object cast to the expected type.</returns>
		/// <exception cref="InvalidOperationException">Thrown when the data object is not of the expected type.</exception>
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
