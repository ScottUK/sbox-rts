﻿using Sandbox;
using System;
using System.Collections.Generic;

namespace Facepunch.RTS
{
    public abstract class BaseItem
	{
		public uint NetworkId { get; set; }
		public virtual string Name => "";
		public virtual string UniqueId => "";
		public virtual string Description => "";
		public virtual Color Color => Color.White;
		public virtual Texture Icon => null;
		public virtual int BuildTime => 0;
		public virtual Dictionary<ResourceType, int> Costs => new();
		public virtual HashSet<string> Dependencies => new();

		public bool Has( Player player )
		{
			return player.Dependencies.Contains( NetworkId );
		}

		public bool HasDependencies( Player player )
		{
			foreach ( var v in Dependencies )
			{
				var dependency = RTS.Item.Find<BaseItem>( v );

				if ( dependency == null )
					throw new Exception( "Unable to locate item by id: " + v );

				if ( !player.Dependencies.Contains( dependency.NetworkId ) )
					return false;
			}

			return true;
		}

		public virtual void OnQueued( Player player )
		{

		}

		public virtual void OnUnqueued( Player player )
		{

		}

		public virtual void OnCreated( Player player )
		{

		}

		public virtual ItemCreateError CanCreate( Player player )
		{
			if ( !CanHave( player ) ) return ItemCreateError.Unknown;

			if ( !player.CanAffordItem( this, out var resource ) )
			{
				return resource.ToCreateError();
			}

			return ItemCreateError.Success;
		}

		public virtual bool CanHave( Player player )
		{
			return HasDependencies( player );
		}
	}
}
