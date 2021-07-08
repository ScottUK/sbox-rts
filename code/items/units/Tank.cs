﻿using Sandbox;
using System.Collections.Generic;

namespace Facepunch.RTS.Units
{
	[Library]
	public class Tank : BaseUnit
	{
		public override string Name => "Tank";
		public override string UniqueId => "unit.tank";
		public override string Model => "models/vehicles/tank/tank.vmdl";
		public override string Description => "Fires projectiles that go bang when they hit.";
		public override Texture Icon => Texture.Load( "textures/rts/icons/scout.png" );
		public override bool UseModelPhysics => true;
		public override bool UseRenderColor => true;
		public override float RotateToTargetSpeed => 10f;
		public override string Weapon => "weapon_tank_cannon";
		public override int NodeSize => 200;
		public override float AttackRange => 1500f;
		public override float LineOfSight => 1500f;
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
		public override float Speed => 250f;
		public override int BuildTime => 2;
		public override Dictionary<ResourceType, int> Costs => new()
		{
			[ResourceType.Beer] = 150,
			[ResourceType.Metal] = 100
		};
	}
}
