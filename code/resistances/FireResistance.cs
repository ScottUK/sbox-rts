﻿using Sandbox;

namespace Facepunch.RTS
{
	[Library]
	public class FireResistance : BaseResistance
	{
		public override string Name => "Fire Resistance";
		public override string UniqueId => "resistance.fire";
		public override Texture Icon => Texture.Load( FileSystem.Mounted, "ui/resistances/fire.png" );
		public override string DamageType => "burn";
	}
}
