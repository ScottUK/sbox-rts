﻿using Sandbox;

namespace Facepunch.RTS
{
	[Library( "weapon_plasma_attack_drone" )]
	public partial class PlasmaAttackDroneWeapon : AttackDroneWeapon
	{
		public override int BaseDamage => 4;
		public override string MuzzleFlash => "particles/weapons/muzzle_flash_plasma/muzzle_flash_plasma.vpcf";
		public override string BulletTracer => "particles/weapons/muzzle_flash_plasma/bullet_trace.vpcf";
		public override string DamageType => "plasma";
		public override string SoundName => "smg.plasma.fire";

		public override void Attack()
		{
			if ( Target is ISelectable target && !target.HasStatus<WeakStatus>() )
			{
				target.ApplyStatus<WeakStatus>( new ModifierData()
				{
					Modifier = 0.2f,
					Duration = 2f
				} );
			}

			base.Attack();
		}
	}
}
