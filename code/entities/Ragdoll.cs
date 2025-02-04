﻿using Facepunch.RTS;
using Sandbox;
using System;

namespace Facepunch.RTS
{
	public partial class Ragdoll : ModelEntity, IFogCullable
	{
		public float FadeOutDuration { get; private set; }
		public float FadeOutTime { get; private set; }
		public float TargetAlpha { get; private set; }
		public bool HasBeenSeen { get; set; }
		public bool IsVisible { get; set; }

		public override void Spawn()
		{
			Fog.AddCullable( this );

			base.Spawn();
		}

		protected override void OnDestroy()
		{
			Fog.RemoveCullable( this );
			base.OnDestroy();
		}

		public Ragdoll FadeOut( float duration )
		{
			FadeOutDuration = duration;
			FadeOutTime = Time.Now + duration;

			return this;
		}

		public static Ragdoll From( ModelEntity entity, Vector3 velocity, bool isBullet, bool isBlast, bool isPhysicsImpaact, Vector3 forcePos, Vector3 force, int bone )
		{
			var ragdoll = new Ragdoll();
			var modelName = entity.GetModelName();

			ragdoll.Position = entity.Position;
			ragdoll.Rotation = entity.Rotation;
			ragdoll.Scale = entity.Scale;
			ragdoll.PhysicsEnabled = true;
			ragdoll.UsePhysicsCollision = true;
			ragdoll.EnableAllCollisions = true;
			ragdoll.SetModel( modelName );
			ragdoll.CopyBonesFrom( entity );
			ragdoll.CopyBodyGroups( entity );
			ragdoll.CopyMaterialGroup( entity );
			ragdoll.TakeDecalsFrom( entity );
			ragdoll.EnableHitboxes = true;
			ragdoll.SurroundingBoundsMode = SurroundingBoundsType.Physics;
			ragdoll.RenderColor = entity.RenderColor;

			if ( ragdoll.PhysicsGroup == null )
			{
				throw new Exception( $"Tried to make a ragdoll with {modelName} but it has no physics group!" );
			}

			ragdoll.PhysicsGroup.Velocity = velocity;
			ragdoll.Tags.Add( "ragdoll" );

			foreach ( var child in entity.Children )
			{
				if ( child is not Clothes clothes )
					continue;

				var clothing = new ModelEntity();

				clothing.SetModel( clothes.GetModelName() );
				clothing.SetParent( ragdoll, true );
				clothing.RenderColor = clothes.RenderColor;
			}

			if ( isBullet || isPhysicsImpaact )
			{
				var body = bone > 0 ? ragdoll.GetBonePhysicsBody( bone ) : null;

				if ( body != null )
					body.ApplyImpulseAt( forcePos, force * body.Mass );
				else
					ragdoll.PhysicsGroup.ApplyImpulse( force );
			}

			if ( isBlast )
			{
				if ( ragdoll.PhysicsGroup != null )
				{
					ragdoll.PhysicsGroup.AddVelocity( (entity.Position - (forcePos + Vector3.Down * 100.0f)).Normal * (force.Length * 0.2f) );
					var angularDir = (Rotation.FromYaw( 90 ) * force.WithZ( 0 ).Normal).Normal;
					ragdoll.PhysicsGroup.AddAngularVelocity( angularDir * (force.Length * 0.02f) );
				}
			}

			return ragdoll;
		}

		[Event.Tick]
		public void ClientTick()
		{
			RenderColor = RenderColor.WithAlpha( RenderColor.a.LerpTo( TargetAlpha, Time.Delta * 8f ) );

			if ( FadeOutDuration > 0f )
			{
				var fraction = ((FadeOutTime - Time.Now) / FadeOutDuration).Clamp( 0f, 1f );

				if ( fraction <= 0f )
				{
					Delete();
					return;
				}

				RenderColor = RenderColor.WithAlpha( RenderColor.a * fraction );
			}

			for ( int i = 0; i < Children.Count; i++ )
			{
				var child = Children[i];

				if ( child is ModelEntity model )
				{
					model.RenderColor = model.RenderColor.WithAlpha( RenderColor.a );
				}
			}
		}

		public void OnVisibilityChanged( bool isVisible ) { }

		public void MakeVisible( bool isVisible )
		{
			TargetAlpha = isVisible ? 1f : 0f;
		}
	}
}

