﻿using Sandbox;

namespace Facepunch.RTS.Upgrades
{
	public abstract class BaseWeaponUpgrade : BaseUpgrade
	{
		public override void OnCreated( RTSPlayer player, ISelectable target )
		{
			target.Tags.Add( "weapon_upgrade" );

			base.OnCreated( player, target );
		}

		public override bool IsAvailable( RTSPlayer player, ISelectable target )
		{
			if ( target.Tags.Has( "weapon_upgrade" ) )
				return false;

			return base.IsAvailable( player, target );
		}
	}
}
