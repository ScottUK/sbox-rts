﻿using System.Collections.Generic;
using System;
using Sandbox;

namespace Facepunch.RTS.Units
{
    public abstract class BaseUnit : BaseItem
	{
		public override Color Color => Color.Cyan;
		public virtual float MaxHealth => 100f;
		public virtual string Model => "models/units/simpleterry.vmdl";
		public virtual HashSet<string> Clothing => new();
		public virtual bool CanConstruct => false;
		public virtual bool CanEnterBuildings => false;
		public virtual bool UseModelPhysics => false;
		public virtual HashSet<ResourceType> Gatherables => new();
		public virtual Dictionary<ResourceType, string[]> GatherSounds => new();
		public virtual string[] ConstructSounds => Array.Empty<string>();
		public virtual string[] AttackSounds => Array.Empty<string>();
		public virtual string[] SelectSounds => Array.Empty<string>();
		public virtual string[] DepositSounds => Array.Empty<string>();
		public virtual string[] MoveSounds => Array.Empty<string>();
		public virtual int NodeSize => 50;
		public virtual float Speed => 300f;
		public virtual float RotateToTargetSpeed => 10f;
		public virtual float LineOfSight => 1000f;
		public virtual float ConstructRange => 1500f;
		public virtual float AttackRange => 1000f;
		public virtual float InteractRange => 80f;
		public virtual List<string> DamageDecals => new()
		{
			"materials/rts/blood/blood1.vmat"
		};
		public virtual List<string> ImpactEffects => new()
		{
			"particles/impact.flesh.vpcf"
		};
		public virtual uint Population => 1;
		public virtual bool UseRenderColor => false;
		public virtual string Weapon => "";
		public virtual HashSet<string> Abilities => new();
		public virtual HashSet<string> Buildables => new();

		public override void OnQueued( Player player )
		{
			player.AddPopulation( Population );
		}

		public override void OnUnqueued( Player player )
		{
			player.TakePopulation( Population );
		}

		public override void OnCreated( Player player )
		{
			RTS.Game.Toast( player, "Unit Trained", this );

			base.OnCreated( player );
		}

		public override RequirementError CanCreate( Player player )
		{
			if ( !player.HasPopulationCapacity( Population ) )
				return RequirementError.NotEnoughPopulation;

			return base.CanCreate( player );
		}

		public void PlayConstructSound( Player player )
		{
			if ( ConstructSounds.Length > 0 )
				SoundManager.Play( player, Rand.FromArray( ConstructSounds ) );
		}

		public void PlaySelectSound( Player player )
		{
			if ( SelectSounds.Length > 0 )
				SoundManager.Play( player, Rand.FromArray( SelectSounds ) );
		}

		public void PlayDepositSound( Player player )
		{
			if ( DepositSounds.Length > 0 )
				SoundManager.Play( player, Rand.FromArray( DepositSounds ) );
		}

		public void PlayGatherSound( Player player, ResourceType resource )
		{
			if ( GatherSounds.TryGetValue( resource, out var sounds ) )
				SoundManager.Play( player, Rand.FromArray( sounds ) );
		}

		public void PlayAttackSound( Player player )
		{
			if ( AttackSounds.Length > 0 )
				SoundManager.Play( player, Rand.FromArray( AttackSounds ) );
		}

		public void PlayMoveSound( Player player )
		{
			if ( MoveSounds.Length > 0 )
				SoundManager.Play( player, Rand.FromArray( MoveSounds ) );
		}
	}
}
