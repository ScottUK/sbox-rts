﻿using Facepunch.RTS.Abilities;
using Facepunch.RTS.Ranks;
using Facepunch.RTS.Units;
using Gamelib.Extensions;
using Gamelib.FlowFields;
using Gamelib.FlowFields.Grid;
using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Facepunch.RTS
{
	public partial class UnitEntity : ItemEntity<BaseUnit>, IFogViewer, IFogCullable, IDamageable, IMoveAgent
	{
		private struct AnimationValues
		{
			public float Speed;
			public bool Attacking;
			public int HoldType;

			public void Start()
			{
				Speed = 0f;
				HoldType = 0;
				Attacking = false;
			}

			public void Finish( AnimEntity entity )
			{
				entity.SetAnimInt( "holdtype", HoldType );
				entity.SetAnimBool( "attacking", Attacking );
				entity.SetAnimFloat( "speed", entity.GetAnimFloat( "speed" ).LerpTo( Speed, Time.Delta * 10f ) );
			}
		}

		public override bool HasSelectionGlow => false;
		public override int AttackPriority => 1;

		public Dictionary<ResourceType, int> Carrying { get; private set; }
		[Net, Local] public BuildingEntity Occupying { get; private set; }
		[Net, Local] public float GatherProgress { get; private set; }
		[Net, Local] public bool IsGathering { get; private set; }
		[Net] public Weapon Weapon { get; private set; }
		[Net] public float LineOfSight { get; private set; }
		[Net, OnChangedCallback] public int Kills { get; set; }
		public override bool CanMultiSelect => true;
		public List<ModelEntity> Clothing => new();
		public UnitCircle Circle { get; private set; }
		public TimeSince LastAttackTime { get; set; }
		public Pathfinder Pathfinder { get; private set; }
		public bool HasBeenSeen { get; set; }
		public bool FollowTarget { get; private set; }
		public float TargetAlpha { get; private set; }
		public float AgentRadius { get; private set; }
		public Vector3? TargetPosition { get; private set; }
		public float Speed { get; private set; }
		public Entity Target { get; private set; }
		public bool IsStatic { get; private set; }
		public TimeSince LastGatherTime { get; private set; }
		public ResourceEntity LastResourceEntity { get; private set; }
		public DamageInfo LastDamageTaken { get; private set; }
		public ResourceType LastResourceType { get; private set; }
		public Vector3 LastResourcePosition { get; private set; }
		public MoveGroup MoveGroup { get; private set; }
		public Vector3 InputVelocity { get; private set; }
		public float? SpinSpeed { get; private set; }
		public float TargetRange { get; private set; }
		public BaseRank Rank { get; private set; }

		#region UI
		public EntityHudBar HealthBar { get; private set; }
		public EntityHudBar GatherBar { get; private set; }
		public EntityHudIcon RankIcon { get; private set; }
		#endregion

		private List<ISelectable> _targetBuffer = new();
		private AnimationValues _animationValues;
		private RealTimeUntil _nextFindTarget;

		public UnitEntity() : base()
		{
			Tags.Add( "unit", "selectable", "flowfield" );

			if ( IsServer )
			{
				Carrying = new();
			}

			// Don't collide with anything but static shit.
			CollisionGroup = CollisionGroup.Debris;

			// We start out as a static obstacle.
			IsStatic = true;
		}

		public bool CanConstruct => Item.CanConstruct;

		public void AddKill()
		{
			Host.AssertServer();

			Kills += 1;
			Rank = RankManager.Find( Kills );
		}

		public bool CanGather( ResourceType type )
		{
			return Item.Gatherables.Contains( type );
		}

		public bool IsTargetValid()
		{
			if ( !Target.IsValid() ) return false;

			if ( Target is UnitEntity unit )
			{
				return !unit.Occupying.IsValid();
			}

			return true;
		}

		public void GiveHealth( float health )
		{
			Host.AssertServer();

			Health = Math.Min( Health + health, MaxHealth );
		}

		public void MakeStatic( bool isStatic )
		{
			// Don't update if we don't have to.
			if ( IsStatic == isStatic ) return;

			if ( isStatic )
				Tags.Remove( "flowfield" );
			else
				Tags.Add( "flowfield" );

			Pathfinder.UpdateCollisions( Position, Item.NodeSize * 2f );

			IsStatic = isStatic;
		}

		public bool IsTargetInRange()
		{
			if ( !Target.IsValid() ) return false;

			if ( Target is ModelEntity entity )
			{
				// We can try to see if our range overlaps the bounding box of the target.
				var targetBounds = entity.CollisionBounds + entity.Position;

				if ( targetBounds.Overlaps( Position, TargetRange ) )
					return true;
			}

			return (Target.IsValid() && Target.Position.Distance( Position ) < TargetRange);
		}

		public bool TakeFrom( ResourceEntity resource )
		{
			if ( resource.Stock <= 0 ) return false;

			if ( Carrying.TryGetValue( resource.Resource, out var carrying ) )
			{
				if ( carrying < resource.MaxCarry )
					Carrying[resource.Resource] += 1;
				else
					return false;
			}
			else
			{
				Carrying[resource.Resource] = 1;
			}

			resource.Stock -= 1;

			if ( resource.Stock <= 0 )
				resource.Delete();

			return true;
		}

		public override void OnKilled()
		{
			var damageInfo = LastDamageTaken;

			if ( damageInfo.Attacker is UnitEntity unit )
			{
				unit.AddKill();
			}

			BecomeRagdoll( Velocity, damageInfo.Flags, damageInfo.Position, damageInfo.Force, GetHitboxBone( damageInfo.HitboxIndex ) );

			if ( Occupying.IsValid() )
			{
				Occupying.EvictUnit( this );
			}

			LifeState = LifeState.Dead;
			Delete();
		}

		public override void TakeDamage( DamageInfo info )
		{
			LastDamageTaken = info;

			base.TakeDamage( info );
		}

		public override void StartAbility( BaseAbility ability, AbilityTargetInfo info )
		{
			if ( IsServer ) ClearTarget();

			base.StartAbility( ability, info );
		}

		public override void ClientSpawn()
		{
			Circle = new();
			Circle.Size = GetDiameterXY( 1f, true );
			Circle.SetParent( this );
			Circle.LocalPosition = Vector3.Zero;

			if ( Player.IsValid() && Player.IsLocalPawn )
				FogManager.AddViewer( this );
			else
				FogManager.AddCullable( this );

			base.ClientSpawn();
		}

		public void DoImpactEffects( TraceResult trace )
		{
			var impactEffects = Item.ImpactEffects;
			var particleName = impactEffects[Rand.Int( 0, impactEffects.Count - 1 )];

			if ( particleName != null )
			{
				var particles = Particles.Create( particleName, trace.EndPos );
				particles.SetForward( 0, trace.Normal );
			}
		}

		public void CreateDamageDecals( Vector3 position )
		{
			var damageDecals = Item.DamageDecals;

			if ( damageDecals.Count == 0 ) return;

			var randomDecalName = damageDecals[Rand.Int( 0, damageDecals.Count - 1 )];
			var decalMaterial = Material.Load( randomDecalName );
			var decalRotation = Rotation.LookAt( Vector3.Up ) * Rotation.FromAxis( Vector3.Forward, Rand.Float( 0f, 360f ) );
			var randomSize = Rand.Float( 50f, 100f );
			var trace = Trace.Ray( position, position + Vector3.Down * 100f ).Ignore( this ).Run();

			Decals.Place( decalMaterial, trace.Entity, trace.Bone, trace.EndPos, new Vector3( randomSize, randomSize, 4f ), decalRotation );
		}

		public bool IsInMoveGroup( UnitEntity other )
		{
			return (other.MoveGroup == MoveGroup);
		}

		public void Attack( ISelectable target, bool autoFollow = true )
		{
			Attack( target as Entity, autoFollow );
		}

		public void Attack( Entity target, bool autoFollow = true )
		{
			ResetTarget();
			Target = target;
			SetTargetRange( Item.AttackRange );
			FollowTarget = autoFollow;
			OnTargetChanged();
		}

		public void MoveTo( MoveGroup group )
		{
			ResetTarget();
			SetMoveGroup( group );
			OnTargetChanged();
		}

		public void MoveTo( Vector3 position )
		{
			ResetTarget();
			SetMoveGroup( CreateMoveGroup( position ) );
			OnTargetChanged();
		}

		public MoveGroup CreateMoveGroup( Vector3 destination )
		{
			var moveGroup = new MoveGroup();
			moveGroup.Initialize( this, destination );
			return moveGroup;
		}

		public MoveGroup CreateMoveGroup( List<Vector3> destinations )
		{
			var moveGroup = new MoveGroup();
			moveGroup.Initialize( this, destinations );
			return moveGroup;
		}

		public bool CanOccupy( BuildingEntity building )
		{
			var allowedOccupants = building.Item.AllowedOccupants;

			if ( allowedOccupants.Count == 0 )
				return true;

			return allowedOccupants.Contains( Item.UniqueId );
		}

		public bool Occupy( BuildingEntity building, MoveGroup moveGroup = null )
		{
			moveGroup ??= CreateMoveGroup( GetDestinations( building ) );

			if ( !moveGroup.IsValid() )
			{
				ClearTarget();
				return false;
			}

			ResetTarget();
			Target = building;
			FollowTarget = true;
			SetMoveGroup( moveGroup );
			SetTargetRange( Item.InteractRange );
			OnTargetChanged();

			return true;
		}

		public bool Deposit( BuildingEntity building, MoveGroup moveGroup = null )
		{
			moveGroup ??= CreateMoveGroup( GetDestinations( building ) );

			if ( !moveGroup.IsValid() )
			{
				ClearTarget();
				return false;
			}

			ResetTarget();
			Target = building;
			FollowTarget = true;
			SetMoveGroup( moveGroup );
			SetTargetRange( Item.InteractRange );
			OnTargetChanged();

			return true;
		}

		public bool Gather( ResourceEntity resource, MoveGroup moveGroup = null )
		{
			moveGroup ??= CreateMoveGroup( GetDestinations( resource ) );

			if ( !moveGroup.IsValid() )
			{
				ClearTarget();
				return false;
			}

			ResetTarget();
			Target = resource;
			FollowTarget = true;
			LastResourceType = resource.Resource;
			LastResourceEntity = resource;
			LastResourcePosition = resource.Position;
			SetMoveGroup( moveGroup );
			SetTargetRange( Item.InteractRange );
			OnTargetChanged();

			return true;
		}

		public bool Construct( BuildingEntity building, MoveGroup moveGroup = null )
		{
			moveGroup ??= CreateMoveGroup( GetDestinations( building ) );

			if ( !moveGroup.IsValid() )
			{
				ClearTarget();
				return false;
			}

			ResetTarget();
			Target = building;
			FollowTarget = true;
			SetMoveGroup( moveGroup );
			SetTargetRange( Item.InteractRange );
			OnTargetChanged();

			return true;
		}

		public void ClearTarget()
		{
			Target = null;
			TargetPosition = null;
			IsGathering = false;
			FollowTarget = false;
			ClearMoveGroup();
			OnTargetChanged();
		}

		public float LookAtPosition( Vector3 position, float? interpolation = null )
		{
			var targetDirection = position - Position;
			var targetRotation = Rotation.LookAt( targetDirection.Normal, Vector3.Up );

			if ( interpolation.HasValue )
				Rotation = Rotation.Lerp( Rotation, targetRotation, interpolation.Value );
			else
				Rotation = targetRotation;

			return Rotation.Distance( targetRotation );
		}

		public float LookAtEntity( Entity target, float? interpolation = null )
		{
			return LookAtPosition( target.Position, interpolation );
		}

		public void MakeVisible( bool isVisible )
		{
			TargetAlpha = isVisible ? 1f : 0f;
			UI.SetVisible( isVisible );
		}

		public ModelEntity AttachClothing( string modelName )
		{
			var entity = new Clothes();

			entity.SetModel( modelName );
			entity.SetParent( this, true );

			Clothing.Add( entity );

			return entity;
		}

		public void OnMoveGroupDisposed()
		{
			Target = null;
			TargetPosition = null;
			IsGathering = false;
			FollowTarget = false;
			SetMoveGroup( null );
			OnTargetChanged();
		}

		public List<Vector3> GetDestinations( ModelEntity model )
		{
			// Round up the radius to the nearest node size.
			var radius = MathF.Ceiling( model.GetDiameterXY( 0.5f ) / Pathfinder.NodeSize ) * Pathfinder.NodeSize;
			var potentialTiles = new List<Vector3>();
			var possibleLocations = new List<GridWorldPosition>();

			Pathfinder.GetGridPositions( model.Position, radius, possibleLocations );

			var destinations = possibleLocations.ConvertAll( v =>
			{
				Pathfinder.DrawBox( v, Color.Blue, 10f );
				return Pathfinder.GetPosition( v );
			} );

			return destinations;
		}

		public void RemoveClothing()
		{
			Clothing.ForEach( ( entity ) => entity.Delete() );
			Clothing.Clear();
		}

		public virtual void OnEnterBuilding( BuildingEntity building )
		{
			Deselect();
			SetParent( this );
			Occupying = building;
			EnableDrawing = false;
			EnableAllCollisions = false;
		}

		public virtual void OnLeaveBuilding( BuildingEntity building )
		{
			SetParent( null );
			Occupying = null;
			EnableDrawing = true;
			EnableAllCollisions = true;
		}

		protected override void OnDestroy()
		{
			if ( IsClient )
			{
				Circle?.Delete();
				FogManager.RemoveViewer( this );
				FogManager.RemoveCullable( this );
			}
			else
			{
				if ( Player.IsValid() )
					Player.TakePopulation( Item.Population );

				ClearMoveGroup();
			}

			base.OnDestroy();
		}

		protected override void ServerTick()
		{
			base.ServerTick();

			Velocity = 0;

			_animationValues.Start();

			if ( !Occupying.IsValid() )
			{
				if ( !IsUsingAbility() )
				{
					var isTargetInRange = IsTargetInRange();
					var isTargetValid = IsTargetValid();

					if ( !isTargetValid || !isTargetInRange )
						TickMoveToTarget( isTargetValid );
					else
						TickInteractWithTarget();

					TickFindTarget();
				}
				else
				{
					TickAbility();
				}
			}

			if ( Weapon.IsValid() )
			{
				_animationValues.Attacking = Weapon.LastAttack < 0.1f;
				_animationValues.HoldType = Weapon.HoldType;
			}

			_animationValues.Finish( this );
		}

		protected override void OnItemChanged( BaseUnit item )
		{
			if ( !string.IsNullOrEmpty( item.Model ) )
			{
				SetModel( item.Model );

				var materialGroups = MaterialGroupCount;

				if ( materialGroups > 0 )
					SetMaterialGroup( Rand.Int( 0, materialGroups ) );
			}

			foreach ( var clothes in item.Clothing )
			{
				AttachClothing( clothes );
			}

			Speed = item.Speed;
			Health = item.MaxHealth;
			MaxHealth = item.MaxHealth;
			EyePos = Position + Vector3.Up * 64;
			LineOfSight = item.LineOfSight;
			CollisionGroup = CollisionGroup.Player;
			EnableHitboxes = true;
			Pathfinder = PathManager.GetPathfinder( item.NodeSize );

			if ( item.UseModelPhysics )
				SetupPhysicsFromModel( PhysicsMotionType.Keyframed );
			else
				SetupPhysicsFromCapsule( PhysicsMotionType.Keyframed, Capsule.FromHeightAndRadius( 72, item.NodeSize * 0.5f ) );

			AgentRadius = GetDiameterXY( 2f, true );

			if ( !string.IsNullOrEmpty( item.Weapon ) )
			{
				Weapon = Library.Create<Weapon>( item.Weapon );
				Weapon.Attacker = this;

				var attachment = GetAttachment( "weapon", true );
				
				if ( attachment.HasValue )
				{
					Weapon.SetParent( this );
					Weapon.Position = attachment.Value.Position;
				}
				else 
				{
					Weapon.Position = Position;
					Weapon.SetParent( this, Weapon.BoneMerge );
				}
			}

			base.OnItemChanged( item );
		}

		private void OnKillsChanged()
		{
			Rank = RankManager.Find( Kills );
		}

		private void SetTargetRange( float range )
		{
			TargetRange = range + Pathfinder.NodeSize;
		}

		private void SetMoveGroup( MoveGroup group )
		{
			MoveGroup = group;
		}

		private void ResetTarget()
		{
			Target = null;
			TargetPosition = null;
			IsGathering = false;
			FollowTarget = false;
			ClearMoveGroup();
		}

		private void ClearMoveGroup()
		{
			if ( MoveGroup != null && MoveGroup.IsValid() )
			{
				MoveGroup.Remove( this );
			}

			SetMoveGroup( null );
		}

		private void FindTargetResource()
		{
			// If our last resource entity is valid just use that.
			if ( LastResourceEntity.IsValid() )
			{
				Gather( LastResourceEntity );
				return;
			}

			var entities = Physics.GetEntitiesInSphere( LastResourcePosition, 1000f );

			foreach ( var entity in entities )
			{
				if ( entity is ResourceEntity resource && resource.Resource == LastResourceType )
				{
					Gather( resource );
					return;
				}
			}
		}

		private void FindResourceDepo()
		{
			var buildings = Player.GetBuildings().Where( i => i.Item.CanDepositResources );
			var closestDepo = (BuildingEntity)null;
			var closestDistance = 0f;

			foreach ( var depo in buildings )
			{
				var distance = depo.Position.Distance( Position );

				if ( !closestDepo.IsValid() || distance < closestDistance )
				{
					closestDepo = depo;
					closestDistance = distance;
				}
			}

			if ( closestDepo.IsValid() )
				Deposit( closestDepo );
			else
				ClearTarget();
		}

		private void FindTargetEnemy()
		{
			var entities = Physics.GetEntitiesInSphere( Position, Item.AttackRange * 0.5f );

			_targetBuffer.Clear();

			foreach ( var entity in entities )
			{
				if ( entity is ISelectable selectable && IsEnemy( selectable ) )
				{
					_targetBuffer.Add( selectable );
				}
			}

			_targetBuffer.OrderByDescending( s => s.AttackPriority ).ThenBy( s => s.Position.Distance( Position ) );

			if ( _targetBuffer.Count > 0 )
			{
				Attack( _targetBuffer[0], false );
			}
		}

		private void TickFindTarget()
		{
			if ( !IsSelected && !FollowTarget && Weapon.IsValid() && _nextFindTarget )
			{
				FindTargetEnemy();
				_nextFindTarget = 1;
			}
		}

		private void TickInteractWithTarget()
		{
			var lookAtDistance = 0f;

			if ( SpinSpeed.HasValue )
				Rotation = Rotation.FromYaw( Rotation.Yaw() + SpinSpeed.Value * Time.Delta );
			else
				lookAtDistance = LookAtEntity( Target, Time.Delta * Item.RotateToTargetSpeed );

			if ( Target is BuildingEntity building && building.Player == Player )
			{
				if ( SpinSpeed.HasValue || lookAtDistance.AlmostEqual( 0f, 0.1f ) )
				{
					if ( building.IsUnderConstruction )
						TickConstruct( building );
					else if ( building.CanDepositResources )
						DepositResources();
					else if ( building.CanOccupyUnits )
						TickOccupy( building );
					else
						ClearTarget();
				}
			}
			else if ( Target is ResourceEntity resource )
			{
				if ( SpinSpeed.HasValue || lookAtDistance.AlmostEqual( 0f, 0.1f ) )
				{
					TickGather( resource );
				}
			}
			else if ( Weapon.IsValid() && Weapon.CanAttack() )
			{
				if ( lookAtDistance.AlmostEqual( 0f, Weapon.RotationTolerance ) )
				{
					Weapon.Attack();
				}
			}

			TargetPosition = null;
		}

		private void TickAbility()
		{
			var ability = UsingAbility;

			if ( ability == null || !ability.LookAtTarget )
				return;

			LookAtPosition( ability.TargetInfo.Origin, Time.Delta * Item.RotateToTargetSpeed );
		}

		private void TickMoveToTarget( bool isTargetValid )
		{
			if ( isTargetValid && FollowTarget )
			{
				TargetPosition = Target.Position;
			}

			var steerDirection = Vector3.Zero;
			var pathDirection = Vector3.Zero;
			var movementSpeed = Speed;

			if ( MoveGroup != null && MoveGroup.IsValid() )
			{
				if ( MoveGroup.IsDestination( this, Position ) )
				{
					MoveGroup.Finish( this );
				}
				else
				{
					var direction = MoveGroup.GetDirection( Position );
					pathDirection = direction.Normal.WithZ( 0f );

					if ( MoveGroup.Agents.Count > 1 )
					{
						var flocker = new Flocker();

						// TODO: We should really use the real destination when flocking...
						flocker.Setup( this, MoveGroup.Agents, Position );
						flocker.Flock( Position + direction * Pathfinder.NodeSize );

						steerDirection = flocker.Force.Normal.WithZ( 0f );
					}
				}
			}
			else if ( TargetPosition.HasValue )
			{
				pathDirection = (TargetPosition.Value - Position).Normal.WithZ( 0f );
			}
			else if ( !IsSelected )
			{
				if ( Target is ResourceEntity )
					FindTargetResource();
			}

			if ( pathDirection.Length > 0 )
			{
				if ( Speed >= 300f )
					_animationValues.Speed = 1f;
				else
					_animationValues.Speed = 0.5f;

				// First we'll try our steer direction and see if we can go there.
				if ( steerDirection.Length > 0 )
				{
					var steerVelocity = (steerDirection * Pathfinder.NodeSize);

					if ( Pathfinder.IsAvailable( Position + steerVelocity ) )
					{
						Velocity = (steerDirection * movementSpeed) * Time.Delta;
					}
				}

				if ( Velocity.Length == 0 )
				{
					Velocity = (pathDirection * movementSpeed) * Time.Delta;
				}
			}
			else
			{
				Velocity = 0;
			}

			Position += Velocity;

			AlignToGround();

			var walkVelocity = Velocity.WithZ( 0 );

			if ( walkVelocity.Length > 1 )
			{
				Rotation = Rotation.Lerp( Rotation, Rotation.LookAt( walkVelocity.Normal, Vector3.Up ), Time.Delta * 10f );
			}
		}

		private void AlignToGround()
		{
			Position = Position.WithZ( Pathfinder.GetHeight( Position ) );
		}

		private void TickOccupy( BuildingEntity building )
		{
			if ( building.OccupyUnit( this ) ) ClearTarget();
		}

		private void DepositResources()
		{
			ResourceHint.Send( Player, 2f, Position, Carrying, Color.Green );

			foreach ( var kv in Carrying )
			{
				Player.GiveResource( kv.Key, kv.Value );
			}

			Carrying.Clear();

			FindTargetResource();
		}

		private void TickConstruct( BuildingEntity building )
		{
			building.Health += (building.MaxHealth / building.Item.BuildTime * Time.Delta);
			building.Health = building.Health.Clamp( 0f, building.Item.MaxHealth );

			SpinSpeed = (building.MaxHealth / building.Health) * 200f;
				
			if ( building.Health == building.Item.MaxHealth )
			{
				LookAtEntity( building );
				building.FinishConstruction();
				ClearTarget();
			}
			else
			{
				building.UpdateConstruction();
			}
		}

		private void TickGather( ResourceEntity resource )
		{
			if ( LastGatherTime < resource.GatherTime ) return;

			TakeFrom( resource );

			LastGatherTime = 0;
			IsGathering = true;

			if ( !Carrying.TryGetValue( resource.Resource, out var carrying ) )
				return;

			GatherProgress = (1f / resource.MaxCarry) * carrying;

			if ( carrying < resource.MaxCarry ) return;

			// We're full, let's deposit that shit.
			FindResourceDepo();
		}

		protected override void ClientTick()
		{
			base.ClientTick();

			if ( Occupying.IsValid() )
			{
				Circle.EnableDrawing = false;
				EnableDrawing = false;

				return;
			}

			if ( Circle.IsValid() && Player.IsValid() )
			{
				if ( Player.IsLocalPawn && IsSelected )
					Circle.Color = Color.White;
				else
					Circle.Color = Player.TeamColor;

				Circle.EnableDrawing = true;
			}

			if ( IsLocalPlayers ) return;

			RenderAlpha = RenderAlpha.LerpTo( TargetAlpha, Time.Delta * 8f );

			for ( var i = 0; i < Children.Count; i++ )
			{
				if ( Children[i] is ModelEntity child )
				{
					child.RenderAlpha = RenderAlpha;
				}
			}

			if ( Circle.IsValid() )
			{
				Circle.Alpha = RenderAlpha;
			}

			EnableDrawing = (RenderAlpha > 0f);
		}

		protected virtual void OnTargetChanged()
		{
			if ( Weapon.IsValid() )
				Weapon.Target = Target;

			SpinSpeed = null;
		}

		protected override void AddHudComponents()
		{
			RankIcon = UI.AddChild<EntityHudIcon>( "rank" );
			HealthBar = UI.AddChild<EntityHudBar>( "health" );

			if ( IsLocalPlayers )
			{
				GatherBar = UI.AddChild<EntityHudBar>( "gather" );
			}

			base.AddHudComponents();
		}

		protected override void UpdateHudComponents()
		{
			if ( Health <= MaxHealth * 0.9f )
			{
				HealthBar.Foreground.Style.Width = Length.Fraction( Health / MaxHealth );
				HealthBar.Foreground.Style.Dirty();
				HealthBar.SetClass( "hidden", false );
			}
			else
			{
				HealthBar.SetClass( "hidden", true );
			}

			if ( IsGathering && IsLocalPlayers )
			{
				GatherBar.Foreground.Style.Width = Length.Fraction( GatherProgress );
				GatherBar.Foreground.Style.Dirty();
				GatherBar.SetClass( "hidden", false );
			}
			else
			{
				GatherBar?.SetClass( "hidden", true );
			}

			if ( Rank != null )
			{
				RankIcon.SetClass( "hidden", false );
				RankIcon.Texture = Rank.Icon;
			}
			else
			{
				RankIcon.SetClass( "hidden", true );
			}

			base.UpdateHudComponents();
		}

		[ClientRpc]
		private void BecomeRagdoll( Vector3 velocity, DamageFlags damageFlags, Vector3 forcePos, Vector3 force, int bone )
		{
			Ragdoll.From( this, velocity, damageFlags, forcePos, force, bone ).FadeOut( 10f );
		}
	}
}

