﻿using System;

namespace Xamarin.Forms.Performance.Integration
{
	public class ItemDetailViewModel : BaseViewModel
	{
		public Item Item { get; set; }
		public ItemDetailViewModel (Item item = null)
		{
			Title = item?.Text;
			Item = item;
		}
	}
}
