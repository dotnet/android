using System;

using Android.Content.PM;
using Android.Views;

namespace Android.App
{

	[Serializable]
	[AttributeUsage (AttributeTargets.Class, 
			AllowMultiple=false, 
			Inherited=false)]
	public sealed partial class ActivityAttribute : Attribute, Java.Interop.IJniNameProviderAttribute {

		public ActivityAttribute ()
		{
		}

		public string                 Name                    {get; set;}

#if ANDROID_20
		public bool                   AllowEmbedded           {get; set;}
#endif
		public bool                   AllowTaskReparenting    {get; set;}
		public bool                   AlwaysRetainTaskState   {get; set;}
#if ANDROID_21
		public bool                   AutoRemoveFromRecents   {get; set;}
#endif
#if ANDROID_20
		public string                 Banner                  {get; set;}
#endif
		public bool                   ClearTaskOnLaunch       {get; set;}
#if ANDROID_26
		public string                 ColorMode               {get; set;}
#endif
		public ConfigChanges          ConfigurationChanges    {get; set;}
		public string                 Description             {get; set;}
#if ANDROID_24
		public bool                   DirectBootAware         {get; set;}
#endif
#if ANDROID_21
		public DocumentLaunchMode     DocumentLaunchMode      {get; set;}
#endif
#if ANDROID_24
		public string                 EnableVrMode            {get; set;}
#endif
		public bool                   Enabled                 {get; set;}
		public bool                   ExcludeFromRecents      {get; set;}
		public bool                   Exported                {get; set;}
		public bool                   FinishOnCloseSystemDialogs    {get; set;}
		public bool                   FinishOnTaskLaunch      {get; set;}
#if ANDROID_11
		public bool                   HardwareAccelerated     {get; set;}
#endif
		public string                 Icon                    {get; set;}
		public string                 Label                   {get; set;}
		public LaunchMode             LaunchMode              {get; set;}
#if ANDROID_23
		public string                 LockTaskMode            {get; set;}
#endif
#if ANDROID_11
		public string                 Logo                    {get; set;}
#endif
#if ANDROID_17
		[Obsolete ("There is no //activity/@android:layoutDirection attribute. This was a mistake. " +
				"Perhaps you wanted ConfigurationChanges=ConfigChanges.LayoutDirection?")]
		public LayoutDirection        LayoutDirection         {get; set;}
#endif
		public bool                   MainLauncher            {get; set;}
#if ANDROID_26
		public float                  MaxAspectRatio          {get; set;}
#endif
#if ANDROID_21
		public int                    MaxRecents              {get; set;}
#endif
		public bool                   MultiProcess            {get; set;}
		public bool                   NoHistory               {get; set;}
#if ANDROID_16
		public Type                   ParentActivity          {get; set;}
#endif
		public string                 Permission              {get; set;}
#if ANDROID_21
		public ActivityPersistableMode      PersistableMode   {get; set;}
#endif
		public string                 Process                 {get; set;}
#if ANDROID_26
		public ConfigChanges          RecreateOnConfigChanges    {get; set;}
#endif
#if ANDROID_21
		public bool                   RelinquishTaskIdentity  {get; set;}
#endif
#if ANDROID_24
		public bool                   ResizeableActivity      {get;set;}
#endif
#if ANDROID_21
		public bool                   ResumeWhilePausing      {get; set;}
#endif
#if ANDROID_26
		public WindowRotationAnimation      RotationAnimation    {get; set;}
#endif
#if ANDROID_25
		public string                 RoundIcon               {get; set;}
#endif
#if ANDROID_23
		public bool                   ShowForAllUsers         {get; set;}
#endif
#if ANDROID_17
		[Obsolete ("Please use ShowForAllUsers instead.")]
		public bool                   ShowOnLockScreen        {get; set;}
#endif
#if ANDROID_24
		public bool                   SupportsPictureInPicture {get;set;}
#endif
		public ScreenOrientation      ScreenOrientation       {get; set;}
#if ANDROID_27
		public bool                   ShowWhenLocked          {get; set;}
#endif
#if ANDROID_17
		public bool                   SingleUser              {get; set;}
#endif
		public bool                   StateNotNeeded          {get; set;}
		public string                 TaskAffinity            {get; set;}
		public string                 Theme                   {get; set;}
#if ANDROID_27
		public bool                   TurnScreenOn            {get; set;}
#endif
#if ANDROID_14
		public UiOptions              UiOptions               {get; set;}
#endif
#if ANDROID_26
		public bool                   VisibleToInstantApps    {get; set;}
#endif
		public SoftInput              WindowSoftInputMode     {get; set;}
#if ANDROID_11 // this is not documented on http://developer.android.com/guide/topics/manifest/activity-element.html but on https://developers.google.com/glass/develop/gdk/immersions
		public bool                   Immersive               {get; set;}
#endif
	}
}
