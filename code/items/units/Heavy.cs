﻿using Sandbox;
using System.Collections.Generic;

namespace Facepunch.RTS.Units
{
	[Library]
	public class Heavy : BaseUnit, IInfantryUnit
	{
		public override string Name => "Heavy";
		public override string UniqueId => "unit.heavy";
		public override string Description => "A slow moving but hard hitting heavy machine gunner.";
		public override float ModelScale => 1.15f;
		public override float Speed => 250f;
		public override float MaxHealth => 175f;
		public override Texture Icon => Texture.Load( "ui/icons/assault.png" );
		public override int BuildTime => 20;
		public override OccupantSettings Occupant => new()
		{
			CanAttack = true
		};
		public override HashSet<string> Abilities => new()
		{
			"ability_frenzy"
		};
		public override Dictionary<ResourceType, int> Costs => new()
		{
			[ResourceType.Beer] = 75,
			[ResourceType.Metal] = 50
		};
		public override Dictionary<string, float> Resistances => new()
		{
			["resistance.bullet"] = 0.1f,
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
		public override string Weapon => "weapon_hmg";
		public override HashSet<string> Dependencies => new()
		{

		};
		public override HashSet<string> Queueables => new()
		{
			"upgrade.plasmaheavy"
		};
		public override HashSet<string> Clothing => new()
		{
			"black_boots",
			"tactical_helmet_army",
			"longsleeve",
			"chest_armour",
			"trousers.smart"
		};
	}
}
