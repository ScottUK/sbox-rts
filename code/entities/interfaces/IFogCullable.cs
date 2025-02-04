﻿namespace Facepunch.RTS
{
	public interface IFogCullable
	{
		public bool IsVisible { get; set; }
		public Vector3 Position { get; }
		public BBox CollisionBounds { get; }
		public bool HasBeenSeen { get; set; }
		public void MakeVisible( bool isVisible );
		public void OnVisibilityChanged( bool isVisible );
	}
}
