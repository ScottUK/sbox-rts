﻿using Sandbox;
using System;
using System.Collections.Generic;

namespace Facepunch.RTS.Units
{
	[Library]
	public class Apache : BaseUnit
	{
		public override string Name => "Apache";
		public override string UniqueId => "unit.apache";
		public override string Entity => "unit_apache";
		public override string Model => "models/vehicles/apache/apache.vmdl";
		public override List<ItemLabel> Labels => new()
		{
			new ItemLabel( "Requires Ranged Occupant", Color.Magenta )
		};
		public override string Description => "An armored aircraft that requires one ranged unit to attack.";
		public override Texture Icon => Texture.Load( FileSystem.Mounted, "ui/tempicons/vehicles/apache.png" );
		public override float VerticalOffset => 600f;
		public override float MaxHealth => 250f;
		public override bool UsePathfinder => false;
		public override bool UseRenderColor => true;
		public override bool UseModelPhysics => true;
		public override bool RagdollOnDeath => false;
		public override float AgentRadiusScale => 1.5f;
		public override bool AlignToSurface => false;
		public override string IdleLoopSound => "rts.helicopterloop";
		public override float AttackRadius => 1000f;
		public override float MinAttackDistance => 500f;
		public override float MaxVerticalRange => 1000f;
		public override string DeathParticles => "particles/weapons/explosion_ground_large/explosion_ground_large.vpcf";
		public override float LineOfSightRadius => 2000f;
		public override OccupiableSettings Occupiable => new()
		{
			MaxOccupants = 1,
			DamageScale = 0.2f,
			AttackAttachments = new string[] { "muzzle" },
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
		public override float Speed => 700f;
		public override int BuildTime => 30;
		public override Dictionary<ResourceType, int> Costs => new()
		{
			[ResourceType.Beer] = 100,
			[ResourceType.Metal] = 150
		};
		public override HashSet<string> Dependencies => new()
		{

		};
		public override Dictionary<string, float> Resistances => new()
		{
			["resistance.explosive"] = -0.3f,
			["resistance.bullet"] = 0.6f,
			["resistance.fire"] = 0.2f
		};
	}
}
