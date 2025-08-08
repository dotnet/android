using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;

namespace Xamarin.ProjectTools
{
	/// <summary>
	/// Represents an MSBuild property with a name, value, and optional condition.
	/// Properties are used to configure build settings and can be conditional
	/// based on MSBuild expressions.
	/// </summary>
	/// <remarks>
	/// Properties correspond to MSBuild &lt;PropertyGroup&gt; elements and can be
	/// organized into configuration-specific groups (Debug/Release) or common groups.
	/// Used throughout the test project system to define build configuration.
	/// </remarks>
	/// <seealso cref="PropertyGroup"/>
	/// <seealso cref="XamarinProject.CommonProperties"/>
	/// <seealso cref="XamarinProject.DebugProperties"/>
	/// <seealso cref="XamarinProject.ReleaseProperties"/>
	public class Property
	{
		/// <summary>
		/// Initializes a new instance of the Property class with a static value.
		/// </summary>
		/// <param name="condition">Optional MSBuild condition for when this property applies.</param>
		/// <param name="name">The name of the property.</param>
		/// <param name="value">The value of the property.</param>
		public Property (string condition, string name, string value)
			: this (condition, name, () => value)
		{
		}

		/// <summary>
		/// Initializes a new instance of the Property class with a dynamic value.
		/// </summary>
		/// <param name="condition">Optional MSBuild condition for when this property applies.</param>
		/// <param name="name">The name of the property.</param>
		/// <param name="value">A function that returns the value of the property.</param>
		public Property (string condition, string name, Func<string> value)
		{
			Condition = condition;
			Name = name;
			Value = value;
		}
		
		/// <summary>
		/// Gets or sets the name of the property.
		/// </summary>
		public string Name { get; set; }
		
		/// <summary>
		/// Gets or sets a function that returns the value of the property.
		/// </summary>
		public Func<string> Value { get; set; }
		
		/// <summary>
		/// Gets or sets the MSBuild condition that determines when this property applies.
		/// </summary>
		/// <remarks>
		/// If null or empty, the property applies unconditionally. Otherwise, it applies
		/// only when the MSBuild condition evaluates to true.
		/// </remarks>
		public string Condition { get; set; }
	}
}
