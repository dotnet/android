using System;
using System.ComponentModel;

using Android.Content.PM;
using Android.Views;

namespace Android.App {

	[Serializable]
	[AttributeUsage (AttributeTargets.Assembly | AttributeTargets.Class, 
			AllowMultiple=false, 
			Inherited=false)]
	public sealed partial class ApplicationAttribute : Attribute, Java.Interop.IJniNameProviderAttribute {

		public ApplicationAttribute ()
		{
		}

		public string                 Name                    {get; set;}

		public bool                   AllowBackup             {get; set;}
		public bool                   AllowClearUserData      {get; set;}
		public bool                   AllowTaskReparenting    {get; set;}
#if ANDROID_8
		public Type                   BackupAgent             {get; set;}
#endif
#if ANDROID_24
		public bool                   BackupInForeground      {get; set;}
#endif
#if ANDROID_21
		public string                 Banner                  {get; set;}
#endif
		public bool                   Debuggable              {get; set;}
		[Category("@string")]
		public string                 Description             {get; set;}
#if ANDROID_24
		public bool                   DirectBootAware         {get; set;}
#endif
		public bool                   Enabled                 {get; set;}
#if ANDROID_23
		public bool                   ExtractNativeLibs       {get; set;}
		public bool                   FullBackupContent       {get; set;}
#endif
#if ANDROID_21
		public bool                   FullBackupOnly          {get; set;}
#endif
#if ANDROID_11
		public bool                   HardwareAccelerated     {get; set;}
#endif
		public bool                   HasCode                 {get; set;}
		[Category("@drawable;@mipmap")]
		public string                 Icon                    {get; set;}
		public bool                   KillAfterRestore        {get; set;}
#if ANDROID_11
		public bool                   LargeHeap               {get; set;}
#endif
		[Category("@string")]
		public string                 Label                   {get; set;}
#if ANDROID_11
		[Category("@drawable;@mipmap")]
		public string                 Logo                    {get; set;}
#endif
		public Type                   ManageSpaceActivity     {get; set;}
#if ANDROID_26
		public string                 NetworkSecurityConfig   {get; set;}
#endif 
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
#if ANDROID_25
		[Category("@drawable;@mipmap")]
		public string                 RoundIcon               {get; set;}
#endif
#if ANDROID_17
		public bool                   SupportsRtl             {get; set;}
#endif
		public string                 TaskAffinity            {get; set;}
		[Category("@style")]
		public string                 Theme                   {get; set;}
#if ANDROID_14
		public UiOptions              UiOptions               {get; set;}
#endif
#if ANDROID_23
		public bool                   UsesCleartextTraffic    {get; set;}
#endif
#if ANDROID_10
		public bool                   VMSafeMode              {get; set;}
#endif
#if ANDROID_24
		public bool                   ResizeableActivity      {get; set;}
#endif
	}
}
