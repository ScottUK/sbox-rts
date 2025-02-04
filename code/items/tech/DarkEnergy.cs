﻿using Sandbox;
using System.Collections.Generic;

namespace Facepunch.RTS.Tech
{
	[Library]
	public class DarkEnergy : BaseTech
	{
		public override string Name => "Dark Energy";
		public override string UniqueId => "tech.darkenergy";
		public override string Description => "Unlock the secrets of plasma for use in warfare.";
		public override Texture Icon => Texture.Load( FileSystem.Mounted, "ui/icons/wheels.png" );
		public override int BuildTime => 80;
		public override Dictionary<ResourceType, int> Costs => new()
		{
			[ResourceType.Beer] = 100,
			[ResourceType.Metal] = 100,
			[ResourceType.Plasma] = 100
		};
		public override HashSet<string> Dependencies => new()
		{
			"tech.pyrotechnics",
			"tech.cryogenics",
			"tech.extraction"
		};
	}
}
