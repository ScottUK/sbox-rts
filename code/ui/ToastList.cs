﻿
using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;
using System;

namespace Facepunch.RTS
{
	public class ToastItem : Panel
	{
		public Label Text { get; set; }
		public Image Icon { get; set; }

		private float EndTime;

		public ToastItem()
		{
			Icon = Add.Image( "", "icon" );
			Text = Add.Label( "", "text" );
		}

		public void Update( string text, Texture icon = null )
		{
			Icon.Texture = icon;
			Text.Text = text;

			Icon.SetClass( "hidden", icon == null );

			EndTime = Time.Now + 3f;
		}

		public override void Tick()
		{
			if ( !IsDeleting && Time.Now >= EndTime )
				Delete();
		}
	}

	[StyleSheet( "/ui/ToastList.scss" )]
	public class ToastList : Panel
	{
		public static ToastList Instance { get; private set; }

		public ToastList()
		{
			Instance = this;
		}

		public void AddItem( string text, Texture icon = null )
		{
			var item = AddChild<ToastItem>();
			item.Update( text, icon );
			Sound.FromScreen( "toast.show" );
		}
	}
}
