﻿using Gamelib.Extensions;
using Gamelib.FlowFields;
using Sandbox;
using Editor;

namespace Facepunch.RTS
{
	/// <summary>
	/// An entity to block units from passing through, They must destroy the obstacle to pass.
	/// </summary>
	/// 
	[Library( "rts_obstacle") ]
	[Title( "Obstacle" )]
	[Model( Model = "models/rocks/rock_large_00.vmdl" )]
	public partial class ObstacleEntity : ModelEntity, IDamageable, IFogCullable, IHudEntity, ITooltipEntity
	{
		[Property, Net] public string TooltipName { get; set; } = "Obstacle";
		[Property, Net] public float MaxHealth { get; set; } = 500;
		[Property] public string DestroySound { get; set; } = "rts.buildingexplode1";
		[Property] public string DestroyEffect { get; set; } = "particles/destruction_temp/destruction_temp.vpcf";

		public EntityHudAnchor Hud { get; private set; }
		public Vector3 LocalCenter { get; private set; }
		public bool IsLocalPlayers => false;
		public bool HasBeenSeen { get; set; }
		public bool IsVisible { get; set; }

		#region UI
		public EntityHudBar HealthBar { get; private set; }
		#endregion

		public ObstacleEntity() : base()
		{
			Tags.Add( "obstacle" );
		}

		public void Kill()
		{
			LifeState = LifeState.Dead;
			Delete();
		}

		public void MakeVisible( bool isVisible )
		{
			Hud.SetActive( isVisible );

			if ( isVisible )
			{
				Fog.RemoveCullable( this );
			}
		}

		public void OnVisibilityChanged( bool isVisible ) { }

		public void UpdateCollisions()
		{
			var radius = this.GetDiameterXY( 0.75f );

			foreach ( var pathfinder in PathManager.All )
			{
				pathfinder.UpdateCollisions( Position, radius );
			}
		}

		public virtual void ShowTooltip()
		{
			if ( HasBeenSeen )
			{
				ItemTooltip.Instance.Update( this );
				ItemTooltip.Instance.Hover( this );
				ItemTooltip.Instance.Show( 0.5f );
			}
		}

		public virtual bool CanBeAttacked()
		{
			return true;
		}

		public virtual bool ShouldUpdateHud()
		{
			return EnableDrawing && Hud.IsActive;
		}

		public virtual void UpdateHudComponents()
		{
			HealthBar.SetProgress( Health / MaxHealth );
			HealthBar.SetActive( true );
		}

		public override void Spawn()
		{
			SetupPhysicsFromModel( PhysicsMotionType.Keyframed );

			LocalCenter = CollisionBounds.Center;
			Health = MaxHealth;

			base.Spawn();
		}

		public override void ClientSpawn()
		{
			Fog.AddCullable( this );

			Hud = EntityHud.Instance.Create( this );
			Hud.SetActive( false );

			AddHudComponents();

			LocalCenter = CollisionBounds.Center;

			base.ClientSpawn();
		}

		public override void OnKilled()
		{
			Kill();
		}

		public override void TakeDamage( DamageInfo info )
		{
			base.TakeDamage( info );
		}

		public void DoImpactEffects( Vector3 position, Vector3 normal )
		{
			
		}

		public void CreateDamageDecals( Vector3 position )
		{
			
		}

		protected virtual void AddHudComponents()
		{
			HealthBar = Hud.AddChild<EntityHudBar>( "health" );
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();

			if ( Game.IsClient )
			{
				Fog.RemoveCullable( this );
				return;
			}

			if ( Game.IsServer )
			{
				if ( !string.IsNullOrEmpty( DestroySound ) )
				{
					Audio.PlayAll( DestroySound, Position );
				}

				if ( !string.IsNullOrEmpty( DestroyEffect ) )
				{
					var particles = Particles.Create( DestroyEffect );
					particles.SetPosition( 0, Position );
					particles.SetPosition( 1, new Vector3( this.GetDiameterXY( 1f, false ) * 0.75f, 0f, 0f ) );
				}

				UpdateCollisions();
			}
		}
	}
}
