﻿using Facepunch.RTS;
using Gamelib.Extensions;
using Gamelib.FlowFields;
using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Facepunch.RTS
{
	public partial class ResourceEntity : ModelEntity, IFogCullable, IMapIconEntity
	{
		public virtual ResourceType Resource => ResourceType.Stone;
		public virtual int DefaultStock => 250;
		public virtual string Description => "";
		public virtual string ResourceName => "";
		public virtual float GatherTime => 0.25f;
		public virtual string[] GatherSounds => new string[]
		{
			"minerock1",
			"minerock2",
			"minerock3",
			"minerock4"
		};
		public virtual int MaxCarry => 10;

		[Property, Net] public int Stock { get; set; }

		public bool IsLocalPlayers => false;
		public bool HasBeenSeen { get; set; }
		public bool IsVisible { get; set; }
		public bool HasMapIcon { get; private set; }

		public Color IconColor => Resource.GetColor();

		private RealTimeUntil _nextGatherSound;

		public void OnVisibilityChanged( bool isVisible )
		{
			if ( isVisible )
			{
				Fog.RemoveCullable( this );
			}
		}

		public void MakeVisible( bool isVisible ) { }

		public void PlayGatherSound()
		{
			if ( !_nextGatherSound ) return;

			if ( GatherSounds.Length > 0 )
				PlaySound( Rand.FromArray( GatherSounds ) );

			_nextGatherSound = 0.5f;
		}

		public void ShowOutline()
		{
			GlowColor = Resource.GetColor() * 0.1f;
			GlowState = GlowStates.GlowStateOn;
			GlowActive = true;
		}

		public void HideOutline()
		{
			GlowActive = false;
		}
		
		public override void ClientSpawn()
		{
			Fog.AddCullable( this );

			// We only want to add the resource to the map if there's no other nearby resources.
			var others = Physics.GetEntitiesInSphere( Position, 1500f )
				.OfType<ResourceEntity>()
				.Where( e => e.Resource == Resource && e.HasMapIcon );

			if ( !others.Any() )
			{
				var icon = MiniMap.Instance.AddEntity( this, Resource.ToString().ToLower() );
				icon.AddClass( "resource" );
			}

			HasMapIcon = true;

			base.ClientSpawn();
		}

		public override void Spawn()
		{
			base.Spawn();

			SetupPhysicsFromModel( PhysicsMotionType.Static );
			Transmit = TransmitType.Always;

			// Let's make sure there is stock.
			if ( Stock == 0 ) Stock = DefaultStock;
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();

			if ( IsClient )
			{
				MiniMap.Instance.RemoveEntity( this );
				Fog.RemoveCullable( this );
				return;
			}

			var radius = this.GetDiameterXY( 0.75f );

			foreach ( var pathfinder in PathManager.All )
			{
				pathfinder.UpdateCollisions( Position, radius );
			}
		}

		[Event.Tick.Client]
		private void ClientTick()
		{
			if ( Local.Pawn is Player player && player.Position.Distance( Position ) <= 1000f )
				ShowOutline();
			else
				HideOutline();
		}

		public bool ShouldShowOnMap()
		{
			return HasBeenSeen;
		}
	}
}
