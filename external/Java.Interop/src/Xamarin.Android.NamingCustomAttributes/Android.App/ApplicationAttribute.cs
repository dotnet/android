using System;

#if !JCW_ONLY_TYPE_NAMES
using Android.Content.PM;
using Android.Views;
#endif  // !JCW_ONLY_TYPE_NAMES

namespace Android.App {

	[Serializable]
	[AttributeUsage (AttributeTargets.Assembly | AttributeTargets.Class, 
			AllowMultiple=false, 
			Inherited=false)]
#if !JCW_ONLY_TYPE_NAMES
	public
#endif  // !JCW_ONLY_TYPE_NAMES
	sealed partial class ApplicationAttribute : Attribute {

		public ApplicationAttribute ()
		{
		}

		public string                 Name                    {get; set;}

#if !JCW_ONLY_TYPE_NAMES
		public bool                   AllowBackup             {get; set;}
		public bool                   AllowClearUserData      {get; set;}
		public bool                   AllowTaskReparenting    {get; set;}
#if ANDROID_8
		public Type                   BackupAgent             {get; set;}
#endif
#if ANDROID_21
		public string                 Banner                  {get; set;}
#endif
		public bool                   Debuggable              {get; set;}
		public string                 Description             {get; set;}
		public bool                   Enabled                 {get; set;}
#if ANDROID_23
		public bool                   ExtractNativeLibs       {get; set;}
		public bool                   FullBackupContent       {get; set;}
#endif
#if ANDROID_11
		public bool                   HardwareAccelerated     {get; set;}
#endif
		public bool                   HasCode                 {get; set;}
		public string                 Icon                    {get; set;}
		public bool                   KillAfterRestore        {get; set;}
#if ANDROID_11
		public bool                   LargeHeap               {get; set;}
#endif
		public string                 Label                   {get; set;}
#if ANDROID_11
		public string                 Logo                    {get; set;}
#endif
		public Type                   ManageSpaceActivity     {get; set;}
		public string                 Permission              {get; set;}
		public bool                   Persistent              {get; set;}
		public string                 Process                 {get; set;}
#if ANDROID_18
		public string                 RequiredAccountType     {get; set;}
#endif
		public bool                   RestoreAnyVersion       {get; set;}
#if ANDROID_18
		public string                 RestrictedAccountType   {get; set;}
#endif
#if ANDROID_17
		public bool                   SupportsRtl             {get; set;}
#endif
		public string                 TaskAffinity            {get; set;}
		public string                 Theme                   {get; set;}
#if ANDROID_14
		public UiOptions              UiOptions               {get; set;}
#endif
#if ANDROID_10
		public bool                   VMSafeMode              {get; set;}
#endif
#if ANDROID_24
		public bool                   ResizeableActivity      {get; set;}
#endif
#endif  // JCW_ONLY_TYPE_NAMES
	}
}
