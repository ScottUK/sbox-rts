﻿using Sandbox;
using System;
using System.Linq;

namespace Facepunch.RTS
{
	[Library("weapon_hellfire")]
	public partial class HellfireWeapon : Weapon
	{
		public override float FireRate => 2f;
		public override int BaseDamage => 50;
		public override bool BoneMerge => false;
		public override string MuzzleFlash => null;
		public override string BulletTracer => null;
		public override string SoundName => null;
		public override float Force => 5f;

		public override async void Attack()
		{
			LastAttack = 0f;

			PlaySound( "rocketlauncher.fire" );

			for ( var i = 1; i <= 5; i++ )
			{
				var attachment = $"muzzle{i}";
				var transform = Attacker.GetAttachment( attachment, true );
				var rocket = new Projectile
				{
					ExplosionEffect = "particles/weapons/explosion_ground_small/explosion_ground_small.vpcf",
					TrailEffect = "particles/weapons/rocket_trail/rocket_trail.vpcf",
					HitSound = "rocket.explode1",
					BezierCurve = false,
					Debug = false
				};

				rocket.Initialize( transform.Value.Position, Target, Game.Random.Float( FireRate * 0.3f, FireRate * 0.6f ), OnMissileHit );

				await GameTask.Delay( Game.Random.Int( 100, 200 ) );

				if ( !Target.IsValid() ) return;
			}
		}

		public override Transform? GetMuzzle()
		{
			return Attacker?.GetAttachment( "muzzle2", true ) ?? null;
		}

		private void OnMissileHit( Projectile projectile, Entity target )
		{
			if ( target.IsValid() && Attacker.IsValid() )
			{
				DamageEntity( target, "blast", 5f, GetDamage() / 5f );
			}
		}
	}
}
