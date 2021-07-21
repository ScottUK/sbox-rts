﻿using Facepunch.RTS;
using Gamelib.Maths;
using Sandbox;
using System;

namespace Facepunch.RTS
{
	[Library( "weapon_grenade_launcher" )]
	public partial class GrenadeLauncher : Weapon
	{
		public override string BulletTracer => null;
		public override float FireRate => 2f;
		public override int BaseDamage => 15;
		public override int HoldType => 2;

		private Grenade Grenade { get; set; }

		public override void Spawn()
		{
			base.Spawn();

			SetModel( "weapons/rust_smg/rust_smg.vmdl" );
		}

		public override void Attack()
		{
			LastAttack = 0f;

			ShootEffects();
			LaunchProjectile();

			//PlaySound( "rust_pistol.shoot" );
		}

		protected void LaunchProjectile()
		{
			var muzzle = GetMuzzle();

			if ( muzzle.HasValue )
			{
				Grenade = new Grenade();
				Grenade.Initialize( muzzle.Value.Position, Target, 1f, OnGrenadeHit );
			}
		}

		[ClientRpc]
		public override void ShootEffects()
		{
			Host.AssertClient();

			base.ShootEffects();
		}

		protected override void OnDestroy()
		{
			if ( Grenade.IsValid() )
			{
				Grenade.Delete();
				Grenade = null;
			}

			base.OnDestroy();
		}

		private void OnGrenadeHit( Grenade grenade, Entity entity )
		{
			if ( !entity.IsValid() ) return;
			DamageEntity( entity, DamageFlags.Blast, 10f, GetDamage() );
		}
	}
}
