using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;

namespace Xamarin.ProjectTools
{
	/// <summary>
	/// Contains constant values for commonly used MSBuild item types (build actions).
	/// These constants provide a centralized way to reference standard build actions
	/// used in project files and build items.
	/// </summary>
	/// <remarks>
	/// These build actions correspond to the standard MSBuild item types that determine
	/// how files are processed during the build. Used with <see cref="BuildItem"/> 
	/// and throughout the test project system.
	/// </remarks>
	/// <seealso cref="BuildItem"/>
	/// <seealso cref="AndroidBuildActions"/>
	public static class BuildActions
	{
		/// <summary>
		/// Build action for files that should be included in the project but not compiled or processed.
		/// </summary>
		public const string None = "None";
		
		/// <summary>
		/// Build action for references to other projects in the same solution.
		/// </summary>
		public const string ProjectReference = "ProjectReference";
		
		/// <summary>
		/// Build action for NuGet package references.
		/// </summary>
		public const string PackageReference = "PackageReference";
		
		/// <summary>
		/// Build action for assembly references.
		/// </summary>
		public const string Reference = "Reference";
		
		/// <summary>
		/// Build action for source code files that should be compiled.
		/// </summary>
		public const string Compile = "Compile";
		
		/// <summary>
		/// Build action for files that should be embedded as resources in the assembly.
		/// </summary>
		public const string EmbeddedResource = "EmbeddedResource";
		
		/// <summary>
		/// Build action for content files that should be copied to the output directory.
		/// </summary>
		public const string Content = "Content";
		
		/// <summary>
		/// Build action for folders in the project structure.
		/// </summary>
		public const string Folder = "Folder";
	}
}
