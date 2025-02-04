﻿using Sandbox;
using System;
using System.Collections.Generic;

namespace Facepunch.RTS.Units
{
	[Library]
	public class Buggy : BaseUnit, IVehicleUnit
	{
		public override string Name => "Buggy";
		public override string UniqueId => "unit.buggy";
		public override string Model => "models/vehicles/buggy/buggy.vmdl";
		public override string Description => "A fast vehicle that requires one ranged unit to attack.";
		public override Texture Icon => Texture.Load( FileSystem.Mounted, "ui/tempicons/vehicles/buggy.png" );
		public override List<ItemLabel> Labels => new()
		{
			new ItemLabel( "Requires Ranged Occupant", Color.Magenta )
		};
		public override float MaxHealth => 175f;
		public override bool UseModelPhysics => true;
		public override bool UseRenderColor => true;
		public override float RotateToTargetSpeed => 10f;
		public override bool UseBoundsToAlign => true;
		public override float AgentRadiusScale => 1.5f;
		public override string Entity => "unit_buggy";
		public override int NodeSize => 50;
		public override int CollisionSize => 100;
		public override float AttackRadius => 1000f;
		public override float LineOfSightRadius => 1000f;
		public override bool RagdollOnDeath => false;
		public override string DeathParticles => "particles/weapons/explosion_ground_large/explosion_ground_large.vpcf";
		public override OccupiableSettings Occupiable => new()
		{
			AttackAttachments = new string[] { "muzzle" },
			DamageScale = 0.5f,
			MaxOccupants = 1,
			Enabled = true
		};
		public override string[] MoveSounds => new string[]
		{
			"brute.alright_move_out",
			"brute.as_you_wish",
			"brute.going_there",
			"brute.got_it",
			"brute.lets_do_this",
			"brute.lets_get_it_done"
		};
		public override string[] SelectSounds => new string[]
		{
			"brute.ready",
			"brute.ready2",
			"brute.tell_me_what_to_do",
			"brute.tell_me_what_to_do2",
			"brute.yes_boss"
		};
		public override float Speed => 650f;
		public override int BuildTime => 20;
		public override Dictionary<ResourceType, int> Costs => new()
		{
			[ResourceType.Beer] = 100,
			[ResourceType.Metal] = 100
		};
		public override HashSet<string> Dependencies => new()
		{

		};
		public override Dictionary<string, float> Resistances => new()
		{
			["resistance.explosive"] = -0.3f,
			["resistance.bullet"] = 0.25f,
			["resistance.fire"] = 0.2f
		};
	}
}
