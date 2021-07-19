﻿using Sandbox;
using System.Collections.Generic;

namespace Facepunch.RTS
{
	[Library( "ability_molotov" )]
	public class MolotovAbility : BaseGrenadeAbility
	{
		public override string Name => "Molotov";
		public override string Description => "A grenade that deals area of effect fire damage.";
		public override Texture Icon => Texture.Load( "textures/rts/icons/heal.png" );
		public override float Cooldown => 10f;
		public override float MaxDistance => 750f;
		public override float Duration => 1f;
		public override float AreaOfEffectRadius => 300f;
		public override string StatusEffect => "status_burning";
		public override string AreaEffect => "particles/weapons/molotov/molotov_base.vpcf";
	}
}
