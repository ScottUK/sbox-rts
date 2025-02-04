﻿using Sandbox;
using System;
using Editor;

namespace Facepunch.RTS
{
	[Library( "resource_stone" )]
	[Model( Model = "models/rocks/rock_large_00.vmdl", MaterialGroup = "Rock" )]
	[HammerEntity]
	public partial class StoneResource : ResourceEntity
	{
		public override ResourceType Resource => ResourceType.Stone;
		public override int DefaultStock => 400;
		public override string Description => "You can mine this to gather Stone for your empire.";
		public override string ResourceName => "Rocks";
	}
}
