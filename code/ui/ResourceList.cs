﻿
using Sandbox;
using Sandbox.UI;
using System.Collections.Generic;
using System;

namespace Facepunch.RTS
{
	[StyleSheet( "/ui/ResourceList.scss" )]
	public class ResourceList : Panel
	{
		public Dictionary<ResourceType, ItemResourceValue> Resources { get; private set; }
		public Dictionary<ResourceType, int> Cache { get; private set; }

		public ResourceList()
		{
			Cache = new();
			Resources = new();

			AddResource( ResourceType.Stone );
			AddResource( ResourceType.Beer );
			AddResource( ResourceType.Metal );
			AddResource( ResourceType.Plasma );
		}

		public override void Tick()
		{
			SetClass( "hidden", !Hud.IsLocalPlaying() );

			if ( Game.LocalPawn is RTSPlayer player )
			{
				UpdateResource( player, ResourceType.Stone );
				UpdateResource( player, ResourceType.Beer );
				UpdateResource( player, ResourceType.Metal );
				UpdateResource( player, ResourceType.Plasma );
			}

			base.Tick();
		}

		private void UpdateResource( RTSPlayer player, ResourceType type )
		{
			var amount = player.GetResource( type );
			var cached = Cache[type];

			if ( cached == amount ) return;

			Resources[type].LerpTo( amount, 1f );

			Cache[type] = amount;
		}

		private void AddResource( ResourceType type )
		{
			var resource = AddChild<ItemResourceValue>();
			resource.Update( type, 0 );
			Resources.Add( type, resource );

			Cache.Add( type, 0 );
		}
	}
}
