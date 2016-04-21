using System;

using Android.Content.PM;
using Android.Views;

namespace Android.App {

	[Serializable]
	[AttributeUsage (AttributeTargets.Assembly | AttributeTargets.Class, 
			AllowMultiple=true, 
			Inherited=false)]
	public sealed partial class UsesLibraryAttribute : Attribute {

		public UsesLibraryAttribute ()
		{
		}

		public UsesLibraryAttribute (string name)
		{
			Name = name;
		}

		public UsesLibraryAttribute (string name, bool required) : this (name)
		{
			Required = required;
		}

		public string                 Name                    {get; set;}
		public bool                   Required                {get; set;}
	}
}
