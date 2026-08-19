using System;

namespace Android.App
{
	/// <summary>
	/// Declares a single GL texture compression format that the application supports,
	/// generating a <c>&lt;supports-gl-texture&gt;</c> element in the Android manifest.
	/// </summary>
	/// <remarks>
	/// See the Android documentation for
	/// <see href="https://developer.android.com/guide/topics/manifest/supports-gl-texture-element">&lt;supports-gl-texture&gt;</see>.
	/// </remarks>
	[Serializable]
	[AttributeUsage (AttributeTargets.Assembly, 
			AllowMultiple=true, 
			Inherited=false)]
	public sealed partial class SupportsGLTextureAttribute : Attribute
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SupportsGLTextureAttribute" /> class.
		/// </summary>
		/// <param name="name">The GL texture compression format the application supports, for example <c>GL_OES_compressed_ETC1_RGB8_texture</c>.</param>
		public SupportsGLTextureAttribute (string name)
		{
			Name = name;
		}

		/// <summary>
		/// Gets the GL texture compression format the application supports, for example <c>GL_OES_compressed_ETC1_RGB8_texture</c>.
		/// </summary>
		public string                 Name                    {get; private set;}
	}
}

