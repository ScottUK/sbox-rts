﻿using Sandbox;
using System;
using System.Collections.Generic;

namespace Facepunch.RTS
{
	public partial class Weapon : AnimatedEntity
	{
		[Net] public AnimatedEntity Attacker { get; set; }
		[Net] public Entity Occupiable { get; set; }
		[Net] public Entity Target { get; set; }
		public virtual string DamageType => "bullet";
		public virtual WeaponTargetType TargetType => WeaponTargetType.Any;
		public virtual WeaponTargetTeam TargetTeam => WeaponTargetTeam.Enemy;
		public virtual bool BoneMerge => true;
		public virtual bool IsMelee => false;
		public virtual int BaseDamage => 10;
		public virtual int HoldType => 1;
		public virtual float RotationTolerance => 0.5f;
		public virtual string MuzzleFlash => "particles/weapons/muzzle_flash/muzzleflash_base.vpcf";
		public virtual string BulletTracer => "particles/weapons/muzzle_flash/bullet_trace.vpcf";
		public virtual string SoundName => "rust_pistol.shoot";
		public virtual float FireRate => 1f;
		public virtual float Force => 2f;
		public RealTimeUntil NextVisibilityCheck { get;  private set; }
		public TimeSince LastAttack { get; set; }
		public bool IsTargetVisible { get; protected set; }

		public Weapon()
		{
			EnableAllCollisions = false;
		}

		public int GetDamagePerSecond()
		{
			return (GetDamage() / GetFireRate()).CeilToInt();
		}

		public virtual bool CanSeeTarget()
		{
			if ( NextVisibilityCheck )
			{
				var aimRay = GetAimRay();
				var trace = Trace.Ray( aimRay.Position, Target.WorldSpaceBounds.Center )
					.EntitiesOnly()
					.WithoutTags( "obstacle" )
					.WithoutTags( "building" )
					.WithoutTags( "unit" )
					.Ignore( Occupiable )
					.Ignore( Attacker )
					.Run();

				NextVisibilityCheck = 0.5f;

				// Did we make it mostly to our target position?
				IsTargetVisible = ( trace.Fraction >= 0.95f);
			}

			return IsTargetVisible;
		}

		public virtual float GetFireRate()
		{
			var fireRate = FireRate;

			if ( Attacker is UnitEntity unit )
				fireRate *= unit.Modifiers.FireRate;

			return fireRate;
		}

		public virtual int GetDamage()
		{
			var damage = BaseDamage;

			if ( Attacker is UnitEntity unit )
				damage += unit.Modifiers.Damage;

			return damage;
		}

		public virtual bool CanTarget( ISelectable selectable )
		{
			return true;
		}

		public virtual bool CanAttack()
		{
			if ( !CanSeeTarget() ) return false;
			return (LastAttack > GetFireRate());
		}

		public virtual void Dummy( Vector3 position )
		{
			if ( !string.IsNullOrEmpty( SoundName ) )
				PlaySound( SoundName );

			ShootEffects( position );
			LastAttack = 0f;
		}

		public virtual void Attack()
		{
			if ( !string.IsNullOrEmpty( SoundName ) )
				PlaySound( SoundName );

			ShootEffects( Target.WorldSpaceBounds.Center );
			ShootBullet( Force, GetDamage() );
			LastAttack = 0f;
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
					Position = transform.Position,
					Forward = Target.IsValid() ? (Target.Position - transform.Position).Normal : transform.Rotation.Forward.Normal
				};
			}

			return new Ray {
				Position = Position,
				Forward = Target.IsValid() ? (Target.Position - Position).Normal : Rotation.Forward.Normal
			};
		}

		[ClientRpc]
		public virtual void ShootEffects( Vector3 position )
		{
			Game.AssertClient();

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
					tracer.SetPosition( 1, position );
				}
			}
		}

		public void DamageEntity( Entity entity, string damageType, float force, float damage )
		{
			var aimRay = GetAimRay();
			var endPos = entity.WorldSpaceBounds.Center;
			var damageInfo = new DamageInfo()
			{
				Weapon = this,
				Position = endPos,
				Attacker = Attacker,
				Force = aimRay.Forward * 100f * force,
				Damage = damage
			};

			damageInfo = damageInfo.WithTag( damageType );

			entity.TakeDamage( damageInfo );

			if ( entity is IDamageable damageable )
			{
				if ( Game.Random.Float( 1f ) >= 0.5f )
					damageable.DoImpactEffects( endPos, aimRay.Forward );

				if ( Game.Random.Float( 1f ) > 0.7f )
					damageable.CreateDamageDecals( endPos );
			}
		}

		public void DamageTarget( string damageType, float force, float damage )
		{
			DamageEntity( Target, damageType, force, damage );
		}

		public void ShootBullet( float force, float damage )
		{
			DamageTarget( DamageType, force, damage );
		}
	}
}
