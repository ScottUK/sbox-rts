﻿using Sandbox;
using System.Collections.Generic;

namespace Facepunch.RTS.Units
{
	[Library]
	public class Marksman : BaseUnit, IInfantryUnit
	{
		public override string Name => "Marksman";
		public override string UniqueId => "unit.marksman";
		public override string Description => "A stealthy long-ranged Terry ready to scope in on targets.";
		public override float Speed => 250f;
		public override float MaxHealth => 125f;
		public override float AttackRadius => 1200f;
		public override float LineOfSightRadius => 1200f;
		public override Texture Icon => Texture.Load( FileSystem.Mounted, "ui/icons/assault.png" );
		public override int BuildTime => 30;
		public override OccupantSettings Occupant => new()
		{
			CanAttack = true
		};
		public override HashSet<string> Abilities => new()
		{
			"ability_killshot"
		};
		public override Dictionary<ResourceType, int> Costs => new()
		{
			[ResourceType.Beer] = 150,
			[ResourceType.Metal] = 50
		};
		public override Dictionary<string, float> Resistances => new()
		{
			["resistance.fire"] = -0.2f
		};
		public override string[] AttackSounds => new string[]
		{
			"brute.alright",
			"brute.move_it",
			"brute.search_and_destroy",
			"brute.take_em_down"
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
		public override string Weapon => "weapon_sniper";
		public override HashSet<string> Dependencies => new()
		{

		};
		public override HashSet<string> Queueables => new()
		{
			"upgrade.electricsniper",
			"upgrade.plasmasniper"
		};
		public override HashSet<string> Clothing => new()
		{
			"black_boots",
			"tactical_helmet_army",
			"longsleeve",
			"tactical_vest_army",
			"trousers.smart"
		};
	}
}
