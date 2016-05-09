namespace ${ROOT_NAMESPACE}

open System
open System.Collections.Generic
open System.Linq
open System.Text

open Android.App
open Android.Content
open Android.OS
open Android.Runtime
open Android.Views
open Android.Widget

[<Activity (Label = "${PROJECT_NAME}")>]
type ${PROJECT_NAME}() =
  inherit Activity()

  override x.OnCreate(bundle) =
    base.OnCreate (bundle)
    // Create your application here

