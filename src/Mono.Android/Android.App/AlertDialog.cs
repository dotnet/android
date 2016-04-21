using System;
using System.Collections.Generic;

using Android.Content;
using Android.Runtime;
using Android.Widget;

using Java.Interop;

namespace Android.App {

	public partial class AlertDialog {

		public partial class Builder {

			WeakReference weak_implementor_SetOnItemSelectedListener ;

			public event EventHandler<Android.Widget.AdapterView.ItemSelectedEventArgs> ItemSelected {
				add {
					AndroidEventHelper.AddEventHandler<AdapterView.IOnItemSelectedListener, AdapterView.IOnItemSelectedListenerImplementor>(
							ref weak_implementor_SetOnItemSelectedListener,
							CreateItemSelectedImplementor,
							__SetOnItemSelectedListener,
							__h => __h.OnItemSelectedHandler += value);
				}
				remove {
					AndroidEventHelper.RemoveEventHandler<AdapterView.IOnItemSelectedListener, AdapterView.IOnItemSelectedListenerImplementor>(
							ref weak_implementor_SetOnItemSelectedListener,
							AdapterView.IOnItemSelectedListenerImplementor.__IsEmpty,
							__SetOnItemSelectedListener,
							__h => __h.OnItemSelectedHandler -= value);
				}
			}

			public event EventHandler<Android.Widget.AdapterView.NothingSelectedEventArgs> NothingSelected {
				add {
					AndroidEventHelper.AddEventHandler<AdapterView.IOnItemSelectedListener, AdapterView.IOnItemSelectedListenerImplementor>(
							ref weak_implementor_SetOnItemSelectedListener,
							CreateItemSelectedImplementor,
							__SetOnItemSelectedListener,
							__h => __h.OnNothingSelectedHandler += value);
				}
				remove {
					AndroidEventHelper.RemoveEventHandler<AdapterView.IOnItemSelectedListener, AdapterView.IOnItemSelectedListenerImplementor>(
							ref weak_implementor_SetOnItemSelectedListener,
							AdapterView.IOnItemSelectedListenerImplementor.__IsEmpty,
							__SetOnItemSelectedListener,
							__h => __h.OnNothingSelectedHandler -= value);
				}
			}

			void __SetOnItemSelectedListener (AdapterView.IOnItemSelectedListener value)
			{
				SetOnItemSelectedListener (value);
			}

			AdapterView.IOnItemSelectedListenerImplementor CreateItemSelectedImplementor ()
			{
				return new AdapterView.IOnItemSelectedListenerImplementor (this);
			}

			// extra(neous) event
			List<EventHandler> selection_cleared;
			
			void OnSelectionCleared (object o, Android.Widget.AdapterView.NothingSelectedEventArgs args)
			{
				foreach (var h in selection_cleared)
					h (o, EventArgs.Empty);
			}
			
			[Obsolete ("Use NothingSelected event instead")]
			public event EventHandler ItemSelectionCleared {
				add {
					if (selection_cleared == null) {
						selection_cleared = new List<EventHandler> ();
						NothingSelected += OnSelectionCleared;
					}
					selection_cleared.Add (value);
				}
				remove {
					selection_cleared.Remove (value);
				}
			}

			public Android.App.AlertDialog.Builder SetAdapter (Android.Widget.IListAdapter adapter, EventHandler<Android.Content.DialogClickEventArgs> handler)
			{
				return SetAdapter (adapter, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
			}

			public Android.App.AlertDialog.Builder SetCursor (Android.Database.ICursor cursor, EventHandler<Android.Content.DialogClickEventArgs> handler, string labelColumn)
			{
				return SetCursor (cursor, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler }, labelColumn);
			}

			public Android.App.AlertDialog.Builder SetItems (int itemsId, EventHandler<Android.Content.DialogClickEventArgs> handler)
			{
				return SetItems (itemsId, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
			}

			public Android.App.AlertDialog.Builder SetItems (Java.Lang.ICharSequence[] items, EventHandler<Android.Content.DialogClickEventArgs> handler)
			{
				return SetItems (items, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
			}

			public Android.App.AlertDialog.Builder SetItems (string[] items, EventHandler<Android.Content.DialogClickEventArgs> handler)
			{
				return SetItems (items, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
			}

			public Android.App.AlertDialog.Builder SetMultiChoiceItems (int itemsId, bool[] checkedItems, EventHandler<Android.Content.DialogMultiChoiceClickEventArgs> handler)
			{
				return SetMultiChoiceItems (itemsId, checkedItems, new IDialogInterfaceOnMultiChoiceClickListenerImplementor () { Handler = handler });
			}

			public Android.App.AlertDialog.Builder SetMultiChoiceItems (Java.Lang.ICharSequence[] items, bool[] checkedItems, EventHandler<Android.Content.DialogMultiChoiceClickEventArgs> handler)
			{
				return SetMultiChoiceItems (items, checkedItems, new IDialogInterfaceOnMultiChoiceClickListenerImplementor () { Handler = handler });
			}

			public Android.App.AlertDialog.Builder SetMultiChoiceItems (string[] items, bool[] checkedItems, EventHandler<Android.Content.DialogMultiChoiceClickEventArgs> handler)
			{
				return SetMultiChoiceItems (items, checkedItems, new IDialogInterfaceOnMultiChoiceClickListenerImplementor () { Handler = handler });
			}

			public Android.App.AlertDialog.Builder SetMultiChoiceItems (Android.Database.ICursor cursor, string isCheckedColumn, string labelColumn, EventHandler<Android.Content.DialogMultiChoiceClickEventArgs> handler)
			{
				return SetMultiChoiceItems (cursor, isCheckedColumn, labelColumn, new IDialogInterfaceOnMultiChoiceClickListenerImplementor () { Handler = handler });
			}

			public Android.App.AlertDialog.Builder SetNegativeButton (int textId, EventHandler<Android.Content.DialogClickEventArgs> handler)
			{
				return SetNegativeButton (textId, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
			}

			public Android.App.AlertDialog.Builder SetNegativeButton (Java.Lang.ICharSequence text, EventHandler<Android.Content.DialogClickEventArgs> handler)
			{
				return SetNegativeButton (text, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler } );
			}

			public Android.App.AlertDialog.Builder SetNegativeButton (string text, EventHandler<Android.Content.DialogClickEventArgs> handler)
			{
				return SetNegativeButton (text, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
			}

			public Android.App.AlertDialog.Builder SetNeutralButton (int textId, EventHandler<Android.Content.DialogClickEventArgs> handler)
			{
				return SetNeutralButton (textId, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
			}

			public Android.App.AlertDialog.Builder SetNeutralButton (Java.Lang.ICharSequence text, EventHandler<Android.Content.DialogClickEventArgs> handler)
			{
				return SetNeutralButton (text, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
			}

			public Android.App.AlertDialog.Builder SetNeutralButton (string text, EventHandler<Android.Content.DialogClickEventArgs> handler)
			{
				return SetNeutralButton (text, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
			}

			public Android.App.AlertDialog.Builder SetPositiveButton (int textId, EventHandler<Android.Content.DialogClickEventArgs> handler)
			{
				return SetPositiveButton (textId, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
			}

			public Android.App.AlertDialog.Builder SetPositiveButton (Java.Lang.ICharSequence text, EventHandler<Android.Content.DialogClickEventArgs> handler)
			{
				return SetPositiveButton (text, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
			}

			public Android.App.AlertDialog.Builder SetPositiveButton (string text, EventHandler<Android.Content.DialogClickEventArgs> handler)
			{
				return SetPositiveButton (text, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
			}

			public Android.App.AlertDialog.Builder SetSingleChoiceItems (int itemsId, int checkedItem, EventHandler<Android.Content.DialogClickEventArgs> handler)
			{
				return SetSingleChoiceItems (itemsId, checkedItem, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
			}

			public Android.App.AlertDialog.Builder SetSingleChoiceItems (Android.Database.ICursor cursor, int checkedItem, string labelColumn, EventHandler<Android.Content.DialogClickEventArgs> handler)
			{
				return SetSingleChoiceItems (cursor, checkedItem, labelColumn, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
			}

			public Android.App.AlertDialog.Builder SetSingleChoiceItems (Java.Lang.ICharSequence[] items, int checkedItem, EventHandler<Android.Content.DialogClickEventArgs> handler)
			{
				return SetSingleChoiceItems (items, checkedItem, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
			}

			public Android.App.AlertDialog.Builder SetSingleChoiceItems (string[] items, int checkedItem, EventHandler<Android.Content.DialogClickEventArgs> handler)
			{
				return SetSingleChoiceItems (items, checkedItem, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
			}

			public Android.App.AlertDialog.Builder SetSingleChoiceItems (Android.Widget.IListAdapter adapter, int checkedItem, EventHandler<Android.Content.DialogClickEventArgs> handler)
			{
				return SetSingleChoiceItems (adapter, checkedItem, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
			}
		}

		protected AlertDialog (Android.Content.Context context, bool cancelable, EventHandler cancelHandler) 
			: this (context, cancelable, new Android.Content.IDialogInterfaceOnCancelListenerImplementor () { Handler = cancelHandler }) {}

		public void SetButton (int whichButton, Java.Lang.ICharSequence text, EventHandler<Android.Content.DialogClickEventArgs> handler)
		{
			SetButton (whichButton, text, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
		}

		public void SetButton (int whichButton, string text, EventHandler<Android.Content.DialogClickEventArgs> handler)
		{
			SetButton (whichButton, text, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
		}

		public void SetButton (Java.Lang.ICharSequence text, EventHandler<Android.Content.DialogClickEventArgs> handler)
		{
			SetButton (text, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
		}

		public void SetButton (string text, EventHandler<Android.Content.DialogClickEventArgs> handler)
		{
			SetButton (text, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
		}

		public void SetButton2 (Java.Lang.ICharSequence text, EventHandler<Android.Content.DialogClickEventArgs> handler)
		{
			SetButton2 (text, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
		}

		public void SetButton2 (string text, EventHandler<Android.Content.DialogClickEventArgs> handler)
		{
			SetButton2 (text, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
		}

		public void SetButton3 (Java.Lang.ICharSequence text, EventHandler<Android.Content.DialogClickEventArgs> handler)
		{
			SetButton3 (text, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
		}

		public void SetButton3 (string text, EventHandler<Android.Content.DialogClickEventArgs> handler)
		{
			SetButton3 (text, new IDialogInterfaceOnClickListenerImplementor () { Handler = handler });
		}

	}
}

