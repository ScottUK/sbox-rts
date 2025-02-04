﻿using Sandbox;
using System;
using System.Linq;

namespace Facepunch.RTS
{
	[Library("weapon_sam")]
	public partial class SAMWeapon : Weapon
	{
		public override float FireRate => 2f;
		public override int BaseDamage => 25;
		public override bool BoneMerge => false;
		public override string MuzzleFlash => null;
		public override string BulletTracer => null;
		public override string SoundName => null;
		public override float Force => 5f;
		public virtual float RotateSpeed => 20f;

		public Vector3 TargetDirection { get; private set; }
		[Net] public float Recoil { get; private set; }

		public override async void Attack()
		{
			Recoil = 1f;
			LastAttack = 0f;

			PlaySound( "rocketlauncher.fire" );

			for ( var i = 1; i <= 6; i++ )
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

				await GameTask.Delay( Game.Random.Int( 75, 150 ) );

				if ( !Target.IsValid() ) return;
			}
		}

		public override Transform? GetMuzzle()
		{
			return Attacker.GetAttachment( "muzzle2", true );
		}

		public override bool CanAttack()
		{
			return IsFacingTarget() && base.CanAttack();
		}

		[ClientRpc]
		public override void ShootEffects( Vector3 position )
		{
			if ( !IsFacingTarget() ) return;
			
			base.ShootEffects( position );
		}

		private bool IsFacingTarget()
		{
			var selfPosition = Attacker.Position;
			var targetPosition = Target.Position;
			var goalDirection = (targetPosition - selfPosition).Normal;

			if ( TargetDirection.Distance( goalDirection ) > ( 1f / RotateSpeed) )
				return false;

			return true;
		}
		
		[Event.Tick]
		private void UpdateTargetRotation()
		{
			if ( Target.IsValid() )
			{
				TargetDirection = TargetDirection.LerpTo( (Target.Position - Attacker.Position).Normal, Time.Delta * RotateSpeed );
				Attacker.SetAnimParameter( "target", TargetDirection );
			}

			if ( Game.IsServer )
			{
				Attacker.SetAnimParameter( "fire", Recoil );
				Recoil = Recoil.LerpTo( 0f, Time.Delta * 2f );
			}
		}

		private void OnMissileHit( Projectile projectile, Entity target )
		{
			if ( target.IsValid() && Attacker.IsValid() )
			{
				DamageEntity( target, "blast", 5f, GetDamage() / 6f );
			}
		}
	}
}
