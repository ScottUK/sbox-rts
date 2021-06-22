﻿using Sandbox;
using System;

namespace Facepunch.RTS
{
	public partial class RTSCamera : Camera
	{
		public override void Activated()
		{
			var cameraConfig = RTS.Game.Config.Camera;

			if ( Local.Pawn is Player player )
			{
				Pos = player.EyePos;
				Rot = player.EyeRot;
			}

			FieldOfView = cameraConfig.FOV;
			Ortho = cameraConfig.Ortho;

			base.Activated();
		}

		public override void Update()
		{
			if ( Local.Pawn is not Player player ) return;

			var cameraConfig = RTS.Game.Config.Camera;

			if ( cameraConfig.Ortho )
			{
				OrthoSize = 1f + ( (1f - player.ZoomLevel) * cameraConfig.ZoomScale );
				Ortho = true;
			}
			else
			{
				FieldOfView = cameraConfig.FOV;
				Ortho = false;
			}

			Viewer = null;
			Pos = Pos.LerpTo( player.EyePos, Time.Delta * 4f );
			Rot = player.EyeRot;
		}
	}
}
