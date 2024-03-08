using System;

namespace Android.App {

	[Serializable]
	[AttributeUsage (AttributeTargets.Class, 
			AllowMultiple=true, 
			Inherited=false)]
	public sealed partial class IntentFilterAttribute : Attribute {

		public IntentFilterAttribute (string[] actions)
		{
			if (actions == null)
				throw new ArgumentNullException ("actions");
			if (actions.Length < 1)
				throw new ArgumentException ("At least one action must be specified.", "actions");
			Actions = actions;
		}

		public string?    Icon            {get; set;}
		public string?    Label           {get; set;}
		public int        Priority        {get; set;}
		public string[]   Actions         {get; private set;}
		public string[]?  Categories      {get; set;}
		public string?    DataHost        {get; set;}
		public string?    DataMimeType    {get; set;}
		public string?    DataPath        {get; set;}
		public string?    DataPathPattern {get; set;}
		public string?    DataPathPrefix  {get; set;}
		public string?    DataPort        {get; set;}
		public string?    DataScheme      {get; set;}
		public string[]?  DataHosts       {get; set;}
		public string[]?  DataMimeTypes   {get; set;}
		public string[]?  DataPaths       {get; set;}
		public string[]?  DataPathPatterns{get; set;}
		public string[]?  DataPathPrefixes{get; set;}
		public string[]?  DataPorts       {get; set;}
		public string[]?  DataSchemes     {get; set;}
#if ANDROID_23
		// This does not exist on https://developer.android.com/guide/topics/manifest/intent-filter-element.html but on http://developer.android.com/intl/ja/training/app-links/index.html ! (bug #35595)
		public bool       AutoVerify      {get; set;}
#endif
#if ANDROID_25
		public string?    RoundIcon       {get; set;}
#endif
#if ANDROID_26
		public string?    DataPathAdvancedPattern  {get; set;}
		public string[]?  DataPathAdvancedPatterns {get; set;}
#endif
#if ANDROID_31
		public string?    DataPathSuffix {get; set;}
		public string[]?  DataPathSuffixes {get; set;}
#endif
	}
}
