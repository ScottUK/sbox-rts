﻿using Sandbox;
using System;

namespace Facepunch.RTS
{
	[Library("weapon_pistol")]
	partial class Pistol : Weapon, IBallisticsWeapon
	{
		public override float FireRate => 1.0f;
		public override int BaseDamage => 8;
		public override string SoundName => "rts.pistol.shoot";
		public override float Force => 1.5f;

		public override void Spawn()
		{
			base.Spawn();

			SetModel( "models/weapons/pistol/pistol.vmdl" );
		}
	}
}
