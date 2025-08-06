using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;

namespace Xamarin.ProjectTools
{
	/// <summary>
	/// Abstract base class that defines language-specific settings for project generation.
	/// Provides language-specific defaults for file extensions, project types, and template content.
	/// </summary>
	/// <remarks>
	/// Concrete implementations like <see cref="XamarinAndroidProjectLanguage"/> provide
	/// specific settings for different programming languages (C#, F#, etc.).
	/// Used by <see cref="XamarinProject"/> to determine appropriate file extensions and templates.
	/// </remarks>
	/// <seealso cref="XamarinProject.Language"/>
	/// <seealso cref="XamarinAndroidProjectLanguage"/>
	public abstract class ProjectLanguage
	{
		/// <summary>
		/// Gets the default AssemblyInfo file content for this language.
		/// </summary>
		public abstract string DefaultAssemblyInfo { get; }
		
		/// <summary>
		/// Gets the default file extension for source files in this language (e.g., ".cs", ".fs").
		/// </summary>
		public abstract string DefaultExtension { get; }
		
		/// <summary>
		/// Gets the default file extension for designer files in this language.
		/// </summary>
		public abstract string DefaultDesignerExtension { get; }
		
		/// <summary>
		/// Gets the default project file extension for this language (e.g., ".csproj", ".fsproj").
		/// </summary>
		public abstract string DefaultProjectExtension { get; }
		
		/// <summary>
		/// Gets the MSBuild project type GUID that identifies projects of this language.
		/// </summary>
		public abstract string ProjectTypeGuid { get; }
	}

}
