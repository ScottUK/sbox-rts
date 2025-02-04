﻿using System.Collections.Generic;
using System;
using Sandbox;
using Facepunch.RTS;

namespace Facepunch.RTS.Units
{
    public abstract class BaseUnit : BaseItem, IOccupiableItem, IResistorItem
	{
		public override Color Color => Color.Cyan;
		public virtual float MaxHealth => 100f;
		public virtual string Model => "models/units/simpleterry.vmdl";
		public virtual OccupiableSettings Occupiable => new();
		public virtual OccupantSettings Occupant => null;
		public virtual HashSet<string> Clothing => new();
		public virtual float CircleScale => 0.8f;
		public virtual float ModelScale => 1f;
		public virtual float VerticalOffset => 0f;
		public virtual bool UsePathfinder => true;
		public virtual UnitAnimator Animator => new SimpleTerryAnimator();
		public virtual float AgentRadiusScale => 2.5f;
		public virtual bool CanDisband => true;
		public virtual bool CanConstruct => false;
		public virtual bool RagdollOnDeath => true;
		public virtual string DeathParticles => null;
		public virtual bool UseModelPhysics => false;
		public virtual bool AlignToSurface => true;
		public virtual bool UseBoundsToAlign => false;
		public virtual float MaxVerticalRange => 100f;
		public virtual float MinVerticalRange => 0f;
		public virtual float MinAttackDistance => 0f;
		public virtual int MaxCarryMultiplier => 1;
		public virtual int AttackPriority => 1;
		public virtual HashSet<ResourceType> Gatherables => new();
		public virtual Dictionary<ResourceType, string[]> GatherSounds => new();
		public virtual string[] ConstructSounds => Array.Empty<string>();
		public virtual string[] AttackSounds => Array.Empty<string>();
		public virtual string[] SelectSounds => Array.Empty<string>();
		public virtual string[] DepositSounds => Array.Empty<string>();
		public virtual string[] MoveSounds => Array.Empty<string>();
		public virtual string IdleLoopSound => null;
		public virtual int NodeSize => 50;
		public virtual int CollisionSize => 60;
		public virtual float Speed => 300f;
		public virtual float RotateToTargetSpeed => 40f;
		public virtual float LineOfSightRadius => 700f;
		public virtual float ConstructRadius => 0f;
		public virtual float AttackRadius => 700f;
		public virtual Dictionary<string, float> Resistances => new();
		public virtual List<string> DamageDecals => new()
		{
			"materials/rts/blood/blood1.vmat"
		};
		public virtual List<string> ImpactEffects => new()
		{
			"particles/impact.flesh.vpcf"
		};
		public virtual int Population => 1;
		public virtual bool UseRenderColor => false;
		public virtual string Weapon => "";

		public override void OnQueued( RTSPlayer player, ISelectable target )
		{
			player.AddPopulation( Population );
		}

		public override void OnUnqueued( RTSPlayer player, ISelectable target )
		{
			player.TakePopulation( Population );
		}

		public override void OnCreated( RTSPlayer player, ISelectable target )
		{
			Hud.Toast( player, "Unit Trained", this );

			base.OnCreated( player, target );
		}

		public override RequirementError CanCreate( RTSPlayer player, ISelectable target )
		{
			if ( !player.HasPopulationCapacity( Population ) )
				return RequirementError.NotEnoughPopulation;

			return base.CanCreate( player, target );
		}

		public void PlayConstructSound( RTSPlayer player )
		{
			if ( ConstructSounds.Length > 0 )
				Audio.Play( player, Game.Random.FromArray( ConstructSounds ) );
		}

		public void PlaySelectSound( RTSPlayer player )
		{
			if ( SelectSounds.Length > 0 )
				Audio.Play( player, Game.Random.FromArray( SelectSounds ) );
		}

		public void PlayDepositSound( RTSPlayer player )
		{
			if ( DepositSounds.Length > 0 )
				Audio.Play( player, Game.Random.FromArray( DepositSounds ) );
		}

		public void PlayGatherSound( RTSPlayer player, ResourceType resource )
		{
			if ( GatherSounds.TryGetValue( resource, out var sounds ) )
				Audio.Play( player, Game.Random.FromArray( sounds ) );
		}

		public void PlayAttackSound( RTSPlayer player )
		{
			if ( AttackSounds.Length > 0 )
				Audio.Play( player, Game.Random.FromArray( AttackSounds ) );
		}

		public void PlayMoveSound( RTSPlayer player )
		{
			if ( MoveSounds.Length > 0 )
				Audio.Play( player, Game.Random.FromArray( MoveSounds ) );
		}
	}
}
