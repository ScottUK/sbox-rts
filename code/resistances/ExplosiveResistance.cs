﻿using Sandbox;

namespace Facepunch.RTS
{
	[Library]
	public class ExplosiveResistance : BaseResistance
	{
		public override string Name => "Explosive Resistance";
		public override string UniqueId => "resistance.explosive";
		public override Texture Icon => Texture.Load( FileSystem.Mounted, "ui/resistances/explosive.png" );
		public override string DamageType => "blast";
	}
}
