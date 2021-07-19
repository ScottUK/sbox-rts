﻿using Sandbox;
using System;
using System.Collections.Generic;

namespace Facepunch.RTS
{
	public partial class Weapon : AnimEntity
	{
		[Net] public AnimEntity Attacker { get; set; }
		[Net] public Entity Occupiable { get; set; }
		[Net] public Entity Target { get; set; }
		public virtual bool BoneMerge => true;
		public virtual bool IsMelee => false;
		public virtual int BaseDamage => 10;
		public virtual int HoldType => 1;
		public virtual float RotationTolerance => 0.5f;
		public virtual string MuzzleFlash => "particles/weapons/muzzle_flash/muzzleflash_base.vpcf";
		public virtual string BulletTracer => "particles/weapons/muzzle_flash/bullet_trace.vpcf";
		public virtual float FireRate => 1f;
		public TimeSince LastAttack { get; set; }

		public Weapon()
		{
			EnableAllCollisions = false;
		}

		public virtual bool CanSeeTarget()
		{
			var aimRay = GetAimRay();
			var trace = Trace.Ray( aimRay.Origin, Target.WorldSpaceBounds.Center )
				.EntitiesOnly()
				.HitLayer( CollisionLayer.Debris, false )
				.WithoutTags( "unit" )
				.Ignore( Occupiable )
				.Ignore( Attacker )
				.Run();

			// Did we make it mostly to our target position?
			return (trace.Fraction >= 0.95f);
		}

		public virtual int GetDamage()
		{
			if ( Attacker is UnitEntity unit && unit.Rank != null )
			{
				return BaseDamage + unit.Rank.DamageModifier;
			}

			return BaseDamage;
		}

		public virtual bool CanAttack()
		{
			if ( !CanSeeTarget() ) return false;
			return (LastAttack > FireRate);
		}

		public virtual void DummyAttack()
		{
			ShootEffects();
			LastAttack = 0f;
		}

		public virtual void Attack()
		{
			LastAttack = 0f;
			ShootEffects();
			ShootBullet( 1.5f, GetDamage() );
		}

		public virtual Transform? GetMuzzle()
		{
			if ( Occupiable is IOccupiableEntity occupiable )
			{
				var attachment = occupiable.GetAttackAttachment( Target );
				if ( attachment.HasValue ) return attachment;
			}

			return GetAttachment( "muzzle", true );
		}

		public virtual Ray GetAimRay()
		{
			var attachment = GetMuzzle();

			if ( attachment.HasValue )
			{
				var transform = attachment.Value;

				return new Ray {
					Origin = transform.Position,
					Direction = Target.IsValid() ? (Target.Position - transform.Position).Normal : transform.Rotation.Forward.Normal
				};
			}

			return new Ray {
				Origin = Position,
				Direction = Target.IsValid()? (Target.Position - Position).Normal : Rotation.Forward.Normal
			};
		}

		[ClientRpc]
		public virtual void ShootEffects()
		{
			Host.AssertClient();

			if ( IsMelee ) return;

			var muzzle = GetMuzzle();

			if ( muzzle.HasValue )
			{
				if ( !string.IsNullOrEmpty( MuzzleFlash ) )
				{
					var flash = Particles.Create( MuzzleFlash );
					flash.SetPosition( 0, muzzle.Value.Position );
					flash.SetForward( 0, muzzle.Value.Rotation.Forward );
				}

				if ( !string.IsNullOrEmpty( BulletTracer ) )
				{
					var tracer = Particles.Create( BulletTracer );
					tracer.SetPosition( 0, muzzle.Value.Position );
					tracer.SetPosition( 1, Target.WorldSpaceBounds.Center );
				}
			}
		}

		public void DamageEntity( Entity entity, DamageFlags flags, float force, float damage )
		{
			var aimRay = GetAimRay();
			var endPos = entity.WorldSpaceBounds.Center;
			var damageInfo = new DamageInfo()
			{
				Flags = flags,
				Weapon = this,
				Position = endPos,
				Attacker = Attacker,
				Force = aimRay.Direction * 100f * force,
				Damage = damage
			};

			entity.TakeDamage( damageInfo );

			if ( entity is IDamageable damageable )
			{
				if ( Rand.Float( 1f ) >= 0.5f )
					damageable.DoImpactEffects( endPos, aimRay.Direction );

				if ( Rand.Float( 1f ) > 0.7f )
					damageable.CreateDamageDecals( endPos );
			}
		}

		public void DamageTarget( DamageFlags flags, float force, float damage )
		{
			DamageEntity( Target, flags, force, damage );
		}

		public void ShootBullet( float force, float damage )
		{
			DamageTarget( DamageFlags.Bullet, force, damage );
		}
	}
}
