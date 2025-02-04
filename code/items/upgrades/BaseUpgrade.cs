﻿namespace Facepunch.RTS.Upgrades
{
    public abstract class BaseUpgrade : BaseItem
	{
		public override Color Color => Color.Green;

		public virtual string ChangeItemTo => null;
		public virtual string ChangeWeaponTo => null;

		public override bool Has( RTSPlayer player, ISelectable target )
		{
			return target.HasUpgrade( this );
		}

		public override bool IsAvailable( RTSPlayer player, ISelectable target )
		{
			return !target.IsInQueue( this ) && !target.HasUpgrade( this );
		}

		public override void OnCreated( RTSPlayer player, ISelectable target )
		{
			//Audio.Play( player, "announcer.upgrade_complete" );
			Hud.Toast( player, "Upgrade Complete", this );

			base.OnCreated( player, target );
		}
	}
}
