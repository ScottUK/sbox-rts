﻿using Facepunch.RTS;
using Facepunch.RTS.Commands;
using Facepunch.RTS.Units;
using Facepunch.RTS.Upgrades;
using Gamelib.Extensions;
using Gamelib.FlowFields;
using Gamelib.FlowFields.Extensions;
using Gamelib.FlowFields.Grid;
using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Facepunch.RTS
{
	public enum UnitTargetType
	{
		None,
		Move,
		Occupy,
		Repair,
		Construct,
		Gather,
		Deposit,
		Attack
	}

	public partial class UnitEntity : ItemEntity<BaseUnit>, IFogViewer, IFogCullable, IDamageable, IMoveAgent, IOccupiableEntity, IMapIconEntity
	{
		protected class TargetInfo
		{
			public Entity Entity;
			public Vector3? Position;
			public UnitTargetType Type;
			public float Radius;
			public bool Follow;

			public bool HasEntity() => Entity.IsValid();
		}

		protected class GatherInfo
		{
			public ResourceEntity Entity;
			public Vector3 Position;
			public ResourceType Type;
			public TimeSince LastGather;
		}

		public override bool HasSelectionGlow => false;

		public Dictionary<string, float> ResistancesTable { get; set; }
		[Net, Change] private IList<float> ResistanceList { get; set; }
		[Net, Change] public IList<UnitEntity> Occupants { get; private set; }
		public bool CanOccupyUnits => Item.Occupiable.Enabled && Occupants.Count < Item.Occupiable.MaxOccupants;
		public IOccupiableItem OccupiableItem => Item;

		public Dictionary<ResourceType, int> Carrying { get; private set; }
		[Net] public UnitTargetType TargetType { get; protected set; }
		[Net] public Entity TargetEntity { get; protected set; }
		[Net] public Entity Occupiable { get; private set; }
		[Net] public float GatherProgress { get; private set; }
		[Net] public bool IsGathering { get; private set; }
		[Net] public Weapon Weapon { get; private set; }
		[Net] public float LineOfSightRadius { get; private set; }
		[Net] public Vector3 Destination { get; private set; }
		[Net] public TimeSince LastDamageTime { get; private set; }
		[Net, Change] public int Kills { get; set; }
		[Net] public UnitModifiers Modifiers { get; protected set; }
		public override bool CanMultiSelect => true;
		public List<ModelEntity> Clothing => new();
		public UnitCircle Circle { get; private set; }
		public Pathfinder Pathfinder { get; private set; }
		public Color IconColor => Player.TeamColor;
		public bool HasBeenSeen { get; set; }
		public float TargetAlpha { get; private set; }
		public float AgentRadius { get; private set; }
		public bool IsStatic { get; private set; }
		public DamageInfo LastDamageTaken { get; private set; }
		public Stack<MoveGroup> MoveStack { get; private set; }
		public Vector3 TargetVelocity { get; private set; }
		public float? SpinSpeed { get; private set; }
		public bool IsVisible { get; set; }
		public BaseRank Rank { get; private set; }

		#region UI
		public EntityHudIconList OccupantsHud { get; private set; }
		public EntityHudBar HealthBar { get; private set; }
		public EntityHudBar GatherBar { get; private set; }
		public EntityHudIcon RankIcon { get; private set; }
		#endregion

		protected readonly GatherInfo InternalGatherInfo = new();
		protected readonly TargetInfo InternalTargetInfo = new();
		protected IMoveAgent[] BlockBuffer = new IMoveAgent[8];
		protected List<ISelectable> TargetBuffer = new();
		protected Particles PathParticles;
		protected RealTimeUntil NextRepairTime;
		protected RealTimeUntil NextFindTarget;
		protected Sound IdleLoopSound;

		public UnitEntity() : base()
		{
			Tags.Add( "unit", "selectable", "ff_ignore" );

			if ( Game.IsServer )
			{
				Carrying = new();
			}

			ResistanceList = new List<float>();
			ResistancesTable = new();

			Occupants = new List<UnitEntity>();
			MoveStack = new();

			// Create the attribute modifiers object.
			CreateModifiers();

			// We start out as a static obstacle.
			IsStatic = true;
		}

		public bool CanConstruct => Item.CanConstruct;

		public MoveGroup MoveGroup
		{
			get
			{
				if ( MoveStack.TryPeek( out var group ) )
					return group;
				else
					return null;
			}
		}

		public void AddKill()
		{
			if ( Game.IsServer )
			{
				Kills += 1;
				UpdateRank( Ranks.Find( Kills ) );
			}
		}

		public bool CanAttackTarget( IDamageable target )
		{
			if ( target is ISelectable selectable )
				return CanAttackTarget( selectable );

			var entity = target as Entity;

			if ( !InVerticalRange( entity ) )
				return false;

			if ( Weapon.IsValid() )
			{
				if ( Weapon.TargetTeam == WeaponTargetTeam.Ally )
					return false;
			}

			return target.CanBeAttacked();
		}
		
		public bool CanAttackTarget( ISelectable target )
		{
			if ( target == this )
				return false;

			if ( !target.CanBeAttacked() )
				return false;

			if ( !InVerticalRange( target ) )
				return false;

			if ( !Weapon.IsValid() )
				return false;

			if ( Weapon.TargetType == WeaponTargetType.Building && target is not BuildingEntity )
				return false;

			if ( Weapon.TargetType == WeaponTargetType.Unit && target is not UnitEntity )
				return false;

			if ( !Weapon.CanTarget( target ) )
				return false;

			if ( Weapon.TargetTeam == WeaponTargetTeam.Ally )
				return !IsEnemy( target );
			else
				return IsEnemy( target );
		}

		public bool IsAtDestination()
		{
			if ( !MoveStack.TryPeek( out var group ) || !group.IsValid() )
				return true;

			if ( Item.UsePathfinder )
				return group.IsDestination( this, Position );

			var groundPosition = Position.WithZ( 0f );
			var groundDestination = group.GetDestination().WithZ( 0f );
			var tolerance = AgentRadius * 0.1f;

			if ( groundPosition.Distance( groundDestination ) <= tolerance )
				return true;

			return group.IsDestination( this, Position, false );
		}

		public Entity GetTargetEntity() => InternalTargetInfo.Entity;

		public IList<UnitEntity> GetOccupantsList() => (Occupants as IList<UnitEntity>);

		public void UpdateRank( BaseRank rank )
		{
			if ( Rank == rank ) return;

			Rank?.OnTaken( this );
			Rank = rank;
			Rank.OnGiven( this );
		}

		public bool CanGatherAny()
		{
			return Item.Gatherables.Count > 0;
		}

		public bool CanGather( ResourceType type )
		{
			return Item.Gatherables.Contains( type );
		}

		public bool IsTargetValid()
		{
			if ( !InternalTargetInfo.HasEntity() ) return false;

			if ( InternalTargetInfo.Entity is UnitEntity unit )
			{
				return !unit.Occupiable.IsValid();
			}

			return true;
		}

		public void EvictUnit( UnitEntity unit )
		{
			Game.AssertServer();

			if ( Occupants.Contains( unit ) )
			{
				unit.OnVacate( this );
				Occupants.Remove( unit );
			}
		}

		public void EvictAll()
		{
			for ( int i = 0; i < Occupants.Count; i++ )
			{
				var occupant = Occupants[i];
				occupant.OnVacate( this );
			}

			Occupants.Clear();
		}

		public void GiveHealth( float health )
		{
			Game.AssertServer();

			Health = Math.Min( Health + health, MaxHealth );
		}

		public void MakeStatic( bool isStatic )
		{
			// Don't update if we don't have to.
			if ( IsStatic == isStatic ) return;

			if ( isStatic )
				Tags.Remove( "ff_ignore" );
			else
				Tags.Add( "ff_ignore" );

			Pathfinder.UpdateCollisions( Position, Item.NodeSize * 2f );

			IsStatic = isStatic;
		}

		public Transform? GetAttackAttachment( Entity target )
		{
			var attachments = OccupiableItem.Occupiable.AttackAttachments;
			if ( attachments == null ) return null;

			Transform? closestTransform = null;
			var closestDistance = 0f;
			var targetPosition = target.Position;

			for ( var i = 0; i < attachments.Length; i++ )
			{
				var attachment = GetAttachment( attachments[i], true );
				if ( !attachment.HasValue ) continue;

				var position = attachment.Value.Position;
				var distance = targetPosition.Distance( position );

				if ( !closestTransform.HasValue || distance < closestDistance )
				{
					closestTransform = attachment;
					closestDistance = distance;
				}
			}

			return closestTransform;
		}

		public bool IsTargetInRange()
		{
			if ( !InternalTargetInfo.HasEntity() ) return false;

			var target = InternalTargetInfo.Entity;
			var radius = InternalTargetInfo.Radius;

			if ( Occupiable is IOccupiableEntity occupiable )
			{
				var attackRadius = occupiable.GetAttackRadius();

				if ( attackRadius == 0f )
					attackRadius = radius;

				return occupiable.IsInRange( target, attackRadius );
			}

			if ( InternalTargetInfo.Type == UnitTargetType.Attack )
			{
				var minAttackDistance = Item.MinAttackDistance;

				if ( minAttackDistance > 0f )
				{
					var tolerance = (Pathfinder.NodeSize * 2f);

					if ( target.Position.WithZ( 0f ).Distance( Position.WithZ( 0f ) ) < minAttackDistance - tolerance )
						return false;
				}

				return IsInRange( target, radius );
			}
			else
			{
				return IsInRange( target, radius, 1.5f );
			}
		}

		public void Kill( DamageInfo damageInfo = default )
		{
			if ( Item.RagdollOnDeath )
				BecomeRagdoll( Velocity, damageInfo.HasTag( "bullet" ), damageInfo.HasTag( "blast" ), damageInfo.HasTag( "physicsimpact" ), damageInfo.Position, damageInfo.Force, damageInfo.BoneIndex );

			CreateDeathParticles();
			LifeState = LifeState.Dead;
			Delete();
		}

		public bool IsMoveGroupValid()
		{
			if ( MoveStack.TryPeek( out var group ) )
				return group.IsValid();
			else
				return false;
		}

		public void AddResistance( string id, float amount )
		{
			Game.AssertServer();

			var resistance = Resistances.Find( id );

			if ( ResistancesTable.ContainsKey( id ) )
				ResistancesTable[id] += amount;
			else
				ResistancesTable[id] = amount;

			ResistancesTable[id] = ResistancesTable[id].Clamp( -1f, 1f );

			var networkId = resistance.NetworkId;

			while ( ResistanceList.Count <= networkId )
			{
				ResistanceList.Add( 0f );
			}

			ResistanceList[(int)networkId] = ResistancesTable[id];
		}

		public bool TakeFrom( ResourceEntity resource )
		{
			if ( resource.Stock <= 0 ) return false;

			if ( Carrying.TryGetValue( resource.Resource, out var carrying ) )
			{
				var maxCarry = resource.MaxCarry * Item.MaxCarryMultiplier;

				if ( carrying < maxCarry )
					Carrying[resource.Resource] += 1;
				else
					return false;
			}
			else
			{
				Carrying[resource.Resource] = 1;
			}

			resource.PlayGatherSound();
			resource.Stock -= 1;

			if ( resource.Stock <= 0 )
				resource.Delete();

			return true;
		}

		public override int GetAttackPriority()
		{
			return Item.AttackPriority;
		}

		public override bool CanSelect()
		{
			return !Occupiable.IsValid();
		}

		public override void UpdateHudComponents()
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

			OccupantsHud?.SetActive( Occupants.Count > 0 );

			base.UpdateHudComponents();
		}

		public override void OnKilled()
		{
			var damageInfo = LastDamageTaken;

			if ( damageInfo.Attacker is UnitEntity unit )
				unit.AddKill();

			Kill( damageInfo );
		}

		public override void TakeDamage( DamageInfo info )
		{
			info = Resistances.Apply( info, ResistancesTable );

			LastDamageTaken = info;
			LastDamageTime = 0;

			DamageOccupants( info );

			base.TakeDamage( info );
		}

		public virtual bool ShouldShowOnMap()
		{
			return IsLocalTeamGroup || IsVisible;
		}

		public virtual float GetVerticalSpeed()
		{
			return 20f;
		}

		public virtual float GetVerticalOffset()
		{
			var trace = Trace.Ray( Position.WithZ( 1000f ), Position.WithZ( -1000f ) )
				.WorldOnly()
				.Run();

			return trace.EndPosition.z + Item.VerticalOffset;
		}

		private void OnResistanceListChanged()
		{
			for ( var i = 0; i < ResistanceList.Count; i++ )
			{
				var resistance = Resistances.Find<BaseResistance>( (uint)i );
				var uniqueId = resistance.UniqueId;

				if ( ResistanceList[i] != 0f )
					ResistancesTable[uniqueId] = ResistanceList[i];
				else if ( ResistancesTable.ContainsKey( uniqueId ) )
					ResistancesTable.Remove( uniqueId );
			}
		}

		private Vector3 GetGroundNormal()
		{
			var trace = Trace.Ray( Position.WithZ( 1000f ), Position.WithZ( -1000f ) )
				.WorldOnly()
				.Run();

			var normal = trace.Normal;

			if ( Item.UseBoundsToAlign )
			{
				var normals = new Vector3[5];
				var radius = GetDiameterXY( 0.5f );

				normals[0] = normal;

				var bottomLeft = Position + new Vector3( -radius, -radius );
				var bottomRight = Position + new Vector3( -radius, radius );
				var topRight = Position + new Vector3( radius, -radius );
				var topLeft = Position + new Vector3( radius, radius );

				AddGroundNormal( 1, normals, bottomLeft );
				AddGroundNormal( 2, normals, bottomRight );
				AddGroundNormal( 3, normals, topLeft );
				AddGroundNormal( 4, normals, topRight );

				var averaged = Vector3.Zero;
				var count = normals.Length;

				for ( var i = 0; i < count; i++ )
				{
					averaged += normals[i];
				}

				normal = (averaged / count).Normal;
			}

			return normal;
		}

		private void AddGroundNormal( int index, Vector3[] normals, Vector3 position )
		{
			var trace = Trace.Ray( position.WithZ( 1000f ), position.WithZ( -1000f ) )
			.WorldOnly()
			.Run();

			normals[index] = trace.Normal;
		}

		public virtual bool OccupyUnit( UnitEntity unit )
		{
			Game.AssertServer();

			if ( CanOccupyUnits )
			{
				unit.OnOccupy( this );
				Occupants.Add( unit );
				return true;
			}

			return false;
		}

		public virtual bool CanOccupantsAttack()
		{
			return true;
		}

		public override void ClientSpawn()
		{
			Circle = new();

			if ( Item != null )
				Circle.Size = GetDiameterXY( Item.CircleScale, true );
			else
				Circle.Size = GetDiameterXY( 1f, true );

			Circle.SetParent( this );
			Circle.LocalPosition = Vector3.Zero;

			if ( IsLocalTeamGroup )
				Fog.AddViewer( this );
			else
				Fog.AddCullable( this );

			MiniMap.Instance.AddEntity( this, "unit" );

			base.ClientSpawn();
		}

		public void DoImpactEffects( Vector3 position, Vector3 normal )
		{
			var impactEffects = Item.ImpactEffects;
			var particleName = impactEffects[Game.Random.Int( 0, impactEffects.Count - 1 )];

			if ( particleName != null )
			{
				var particles = Particles.Create( particleName, position );
				particles.SetForward( 0, normal );
			}
		}

		public void CreateDamageDecals( Vector3 position )
		{
			/*
			var damageDecals = Item.DamageDecals;

			if ( damageDecals.Count == 0 ) return;

			var randomDecalName = damageDecals[Game.Random.Int( 0, damageDecals.Count - 1 )];
			var decalMaterial = Material.Load( randomDecalName );
			var decalRotation = Rotation.LookAt( Vector3.Up ) * Rotation.FromAxis( Vector3.Forward, Game.Random.Float( 0f, 360f ) );
			var randomSize = Game.Random.Float( 50f, 100f );
			var trace = Trace.Ray( position, position + Vector3.Down * 100f ).Ignore( this ).Run();

			Decals.Place( decalMaterial, trace.Entity, trace.Bone, trace.EndPosition, new Vector3( randomSize, randomSize, 4f ), decalRotation );
			*/
		}

		public bool IsInMoveGroup( UnitEntity other )
		{
			if ( MoveStack.TryPeek( out var a ) && other.MoveStack.TryPeek( out var b ) )
				return (a == b);
			else
				return false;
		}

		public Vector3? GetPerimeterPosition( Vector3 target, float radius )
		{
			var pathfinder = Pathfinder;
			var potentialNodes = new List<Vector3>();
			var searchMultiplier = 1.5f;

			pathfinder.GetGridPositions( Position, radius * searchMultiplier, potentialNodes, true );

			var freeLocations = potentialNodes
				.Where( v => v.Distance( target ) >= radius )
				.OrderBy( v => v.Distance( Position ) )
				.ToList();

			if ( freeLocations.Count > 0 )
				return freeLocations[0];
			else
				return null;
		}

		public void SetAttackTarget( IDamageable target, bool autoFollow = true )
		{
			SetAttackTarget( (ModelEntity)target, autoFollow );
		}

		public void SetAttackTarget( ISelectable target, bool autoFollow = true )
		{
			SetAttackTarget( (ModelEntity)target, autoFollow );
		}

		public void SetAttackTarget( ModelEntity target, bool autoFollow = true )
		{
			ResetTarget();

			InternalTargetInfo.Entity = target;
			InternalTargetInfo.Follow = autoFollow;
			InternalTargetInfo.Radius = Item.AttackRadius;
			InternalTargetInfo.Type = UnitTargetType.Attack;

			OnTargetChanged();
		}

		public void SetMoveTarget( MoveGroup group )
		{
			ResetTarget();

			InternalTargetInfo.Type = UnitTargetType.Move;

			OnTargetChanged();
		}

		public void SetMoveTarget( Vector3 position )
		{
			ResetTarget();

			InternalTargetInfo.Type = UnitTargetType.Move;

			OnTargetChanged();
		}

		public MoveGroup PushMoveGroup( MoveGroup group )
		{
			TryFinishMoveGroup();
			MoveStack.Push( group );
			return group;
		}

		public MoveGroup PushMoveGroup( IMoveCommand command )
		{
			TryFinishMoveGroup();

			var moveGroup = new MoveGroup();
			moveGroup.Initialize( this, command );

			MoveStack.Push( moveGroup );

			return moveGroup;
		}

		public MoveGroup PushMoveGroup( Vector3 destination, IMoveCommand command = null )
		{
			TryFinishMoveGroup();

			var moveGroup = new MoveGroup();
			command ??= new MoveCommand( destination );
			moveGroup.Initialize( this, command );

			MoveStack.Push( moveGroup );

			return moveGroup;
		}

		public MoveGroup PushMoveGroup( List<Vector3> destinations, IMoveCommand command = null )
		{
			if ( destinations.Count == 0 )
				return null;

			TryFinishMoveGroup();

			var moveGroup = new MoveGroup();
			command ??= new MoveCommand( destinations );
			moveGroup.Initialize( this, command );

			MoveStack.Push( moveGroup );

			return moveGroup;
		}

		public void ClearMoveStack()
		{
			foreach ( var group in MoveStack )
			{
				group.Remove( this );
			}

			MoveStack.Clear();
		}

		public bool CanOccupy( IOccupiableEntity occupiable )
		{
			var whitelist = occupiable.OccupiableItem.Occupiable.Whitelist;

			if ( whitelist.Count == 0 )
				return true;

			return whitelist.Contains( Item.UniqueId );
		}

		public bool SetOccupyTarget( IOccupiableEntity occupiable )
		{
			var modelEntity = (occupiable as ModelEntity);

			if ( modelEntity == null )
			{
				ClearTarget();
				return false;
			}

			ResetTarget();

			InternalTargetInfo.Entity = modelEntity;
			InternalTargetInfo.Radius = Pathfinder.NodeSize + Pathfinder.CollisionSize * 2;
			InternalTargetInfo.Type = UnitTargetType.Occupy;

			OnTargetChanged();

			return true;
		}

		public bool SetDepositTarget( BuildingEntity building )
		{
			ResetTarget();

			InternalTargetInfo.Entity = building;
			InternalTargetInfo.Radius = Pathfinder.NodeSize + Pathfinder.CollisionSize * 2;
			InternalTargetInfo.Type = UnitTargetType.Deposit;

			OnTargetChanged();

			return true;
		}

		public bool SetGatherTarget( ResourceEntity resource )
		{
			ResetTarget();

			InternalTargetInfo.Entity = resource;
			InternalTargetInfo.Radius = Pathfinder.NodeSize + Pathfinder.CollisionSize * 2;
			InternalTargetInfo.Type = UnitTargetType.Gather;

			InternalGatherInfo.Type = resource.Resource;
			InternalGatherInfo.Entity = resource;
			InternalGatherInfo.Position = resource.Position;

			OnTargetChanged();

			return true;
		}

		public bool SetRepairTarget( BuildingEntity building )
		{
			ResetTarget();

			InternalTargetInfo.Entity = building;
			InternalTargetInfo.Radius = Pathfinder.NodeSize + Pathfinder.CollisionSize * 2;
			InternalTargetInfo.Type = UnitTargetType.Repair;

			OnTargetChanged();

			return true;
		}

		public bool SetConstructTarget( BuildingEntity building )
		{
			ResetTarget();

			InternalTargetInfo.Entity = building;
			InternalTargetInfo.Radius = Pathfinder.NodeSize + Pathfinder.CollisionSize * 2;
			InternalTargetInfo.Type = UnitTargetType.Construct;

			OnTargetChanged();

			return true;
		}

		public void ClearTarget()
		{
			InternalTargetInfo.Entity = null;
			InternalTargetInfo.Position = null;
			InternalTargetInfo.Follow = false;
			InternalTargetInfo.Type = UnitTargetType.None;

			IsGathering = false;

			OnTargetChanged();
		}

		public float GetAttackRadius() => Item.AttackRadius;
		public float GetMinVerticalRange() => Item.MinVerticalRange;
		public float GetMaxVerticalRange() => Item.MaxVerticalRange;

		public float LookAtPosition( Vector3 position, float? interpolation = null, bool ignoreHeight = true )
		{
			var targetDirection = (position - Position);
			
			if ( ignoreHeight )
			{
				targetDirection = targetDirection.WithZ( 0f );
			}

			var targetRotation = Rotation.LookAt( targetDirection.Normal, Vector3.Up );

			if ( interpolation.HasValue )
				Rotation = Rotation.Lerp( Rotation, targetRotation, interpolation.Value );
			else
				Rotation = targetRotation;

			return Rotation.Distance( targetRotation );
		}

		public float LookAtEntity( Entity target, float? interpolation = null, bool ignoreHeight = true )
		{
			return LookAtPosition( target.Position, interpolation );
		}

		public void OnVisibilityChanged( bool isVisible ) { }

		public void MakeVisible( bool isVisible )
		{
			TargetAlpha = isVisible ? 1f : 0f;
			Hud.SetActive( isVisible );
		}

		public ModelEntity AttachClothing( string modelName )
		{
			var entity = new Clothes();

			entity.SetModel( modelName );
			entity.SetParent( this, true );

			Clothing.Add( entity );

			return entity;
		}

		public void OnMoveGroupDisposed( MoveGroup group )
		{
			if ( MoveStack.TryPeek( out var current ) && current == group )
			{
				InternalTargetInfo.Position = null;
				InternalTargetInfo.Follow = false;

				IsGathering = false;

				OnTargetChanged();

				MoveStack.Pop();

				MoveGroup next = null;

				while ( MoveStack.Count > 0 )
				{
					if ( MoveStack.TryPeek( out next ) )
					{
						if ( ! next.IsValid() )
						{
							MoveStack.Pop();
							continue;
						}
					}

					break;
				}

				if ( next != null )
				{
					next.Resume( this );
					return;
				}

				OnMoveStackEmpty();

				if ( MoveStack.Count == 0 )
					ClearTarget();
			}
		}

		public List<Vector3> GetDestinations( ModelEntity model )
		{
			return model.GetDestinations( Pathfinder );
		}

		public bool InVerticalRange( ISelectable other )
		{
			var entity = (other as Entity);
			if ( !entity.IsValid() ) return false;
			return InVerticalRange( entity );
		}

		public bool InVerticalRange( Entity other )
		{
			var selfPosition = Position;
			var minVerticalRange = Item.MinVerticalRange;
			var maxVerticalRange = Item.MaxVerticalRange;

			if ( Occupiable is IOccupiableEntity occupiable )
			{
				selfPosition = occupiable.Position;
				minVerticalRange = occupiable.GetMinVerticalRange();
				maxVerticalRange = occupiable.GetMaxVerticalRange();
			}

			var distance = Math.Abs( selfPosition.z - other.Position.z );
			return (distance >= minVerticalRange && distance <= maxVerticalRange);
		}

		public float GetSpeed()
		{
			return Item.Speed * Modifiers.Speed;
		}

		public void RemoveClothing()
		{
			Clothing.ForEach( ( entity ) => entity.Delete() );
			Clothing.Clear();
		}

		public Vector3? GetVacatePosition( UnitEntity unit )
		{
			return GetFreePosition( unit, 1.5f );
		}

		public virtual void OnOccupy( IOccupiableEntity occupiable )
		{
			Deselect();
			SetParent( this );

			Occupiable = (Entity)occupiable;
			EnableAllCollisions = false;
			EnableDrawing = false;
		}

		public virtual void OnVacate( IOccupiableEntity occupiable )
		{
			SetParent( null );

			var position = occupiable.GetVacatePosition( this );

			if ( position.HasValue )
			{
				Position = position.Value;
				ResetInterpolation();
			}

			Rotation = Rotation.Identity;
			Occupiable = null;
			EnableAllCollisions = true;
			EnableDrawing = true;
		}

		public virtual void DamageOccupants( DamageInfo info )
		{
			var scale = Item.Occupiable.DamageScale;
			if ( scale <= 0f ) return;

			var occupants = Occupants;
			var occupantsCount = occupants.Count;
			if ( occupantsCount == 0 ) return;

			info.Damage *= scale;

			for ( var i = occupantsCount - 1; i >= 0; i-- )
			{
				var occupant = occupants[i];
				occupant.TakeDamage( info );
			}
		}

		[ClientRpc]
		protected void CreatePathParticles()
		{
			if ( PathParticles != null )
			{
				PathParticles.Destroy( true );
				PathParticles = null;
			}

			if ( !Destination.IsNearZeroLength && !Velocity.IsNearZeroLength )
			{
				PathParticles = Particles.Create( "particles/movement_path/movement_path.vpcf" );
				PathParticles.SetEntity( 0, this );
				PathParticles.SetPosition( 1, GetPathDestination().WithZ( Position.z ) );
				PathParticles.SetPosition( 3, Player.TeamColor * 255f );
			}
		}

		protected void RemovePathParticles()
		{
			if ( PathParticles != null )
			{
				PathParticles.Destroy( true );
				PathParticles = null;
			}
		}

		protected virtual void OnOccupantsChanged()
		{
			if ( OccupantsHud == null ) return;

			OccupantsHud.DeleteChildren( true );

			foreach ( var occupant in Occupants )
			{
				var icon = OccupantsHud.AddChild<EntityHudIcon>();
				icon.Texture = occupant.Item.Icon;
			}
		}

		protected virtual Vector3 GetPathDestination()
		{
			var destination = Destination;

			if ( TargetEntity.IsValid() )
			{
				destination = TargetEntity.WorldSpaceBounds.Center;
			}

			return destination;
		}

		protected override void OnSelected()
		{
			base.OnSelected();

			CreatePathParticles();
		}

		protected override void OnDeselected()
		{
			base.OnDeselected();

			RemovePathParticles();
		}

		protected override void OnQueueItemCompleted( QueueItem queueItem )
		{
			base.OnQueueItemCompleted( queueItem );

			if ( queueItem.Item is BaseUpgrade upgrade )
			{
				var changeWeaponTo = upgrade.ChangeWeaponTo;

				if ( !string.IsNullOrEmpty( changeWeaponTo ) )
				{
					ChangeWeapon( changeWeaponTo );
				}
			}
		}

		protected override void CreateAbilities()
		{
			base.CreateAbilities();

			if ( Item.CanDisband )
			{
				var disbandId = "ability_disband";
				var disband = Abilities.Create( disbandId );
				disband.Initialize( disbandId, this );
				AbilityTable[disbandId] = disband;
			}
		}

		protected override void OnItemNetworkIdChanged()
		{
			base.OnItemNetworkIdChanged();

			if ( Circle != null )
			{
				Circle.Size = GetDiameterXY( Item.CircleScale, true );
			}
		}

		protected override void OnPlayerAssigned( RTSPlayer player )
		{
			if ( Item.UseRenderColor )
			{
				RenderColor = Player.TeamColor;
			}
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();

			if ( Game.IsClient )
			{
				Circle?.Delete();

				Fog.RemoveViewer( this );
				Fog.RemoveCullable( this );

				MiniMap.Instance.RemoveEntity( this );

				RemovePathParticles();

				return;
			}

			if ( Occupiable.IsValid() && Occupiable is IOccupiableEntity occupiable )
				occupiable.EvictUnit( this );

			if ( Player.IsValid() )
				Player.TakePopulation( Item.Population );

			IdleLoopSound.Stop();

			ClearMoveStack();
			EvictAll();
		}

		protected override void ServerTick()
		{
			base.ServerTick();

			Velocity = 0;

			var animator = Item.Animator;
			animator?.Reset();

			if ( !Occupiable.IsValid() )
			{
				if ( !IsUsingAbility() )
				{
					var isTargetInRange = IsTargetInRange();
					var isTargetValid = IsTargetValid();

					if ( InternalTargetInfo.Type == UnitTargetType.Attack && isTargetValid )
					{
						ValidateAttackDistance();
					}

					if ( isTargetValid && isTargetInRange )
						TickInteractWithTarget();
					else
						TickMoveToTarget( isTargetValid, animator );

					TickFindTarget();
				}
				else
				{
					TickAbility();
				}
			}
			else if ( Item.Occupant?.CanAttack == true )
			{
				TickOccupantAttack();
			}

			if ( Weapon.IsValid() )
			{
				if ( animator != null )
				{
					animator.Attacking = Weapon.LastAttack < 0.1f;
					animator.HoldType = Weapon.HoldType;
				}
			}

			// Let's see if our move group has finished now.
			TryFinishMoveGroup();

			// Network the current target type.
			TargetType = InternalTargetInfo.Type;

			animator?.Apply( this );
		}

		protected override void OnItemChanged( BaseUnit item, BaseUnit oldItem )
		{
			if ( !string.IsNullOrEmpty( item.Model ) )
			{
				SetModel( item.Model );

				var materialGroups = MaterialGroupCount;

				if ( materialGroups > 0 )
					SetMaterialGroup( Game.Random.Int( 0, materialGroups ) );
			}

			RemoveClothing();

			var allClothing = ResourceLibrary.GetAll<Clothing>();

			foreach ( var clothes in item.Clothing )
			{
				var modelName = allClothing
					.Where( c => c.ResourceName.ToLower() == clothes.ToLower() )
					.Select( c => c.Model )
					.FirstOrDefault();

				if ( !string.IsNullOrEmpty( modelName ) )
					AttachClothing( modelName );
			}

			Scale = item.ModelScale;
			Health = item.MaxHealth;
			MaxHealth = item.MaxHealth;
			LineOfSightRadius = item.LineOfSightRadius;
			EnableHitboxes = true;

			if ( oldItem  != null )
			{
				// Remove the old base resistances.
				foreach ( var kv in item.Resistances )
					AddResistance( kv.Key, -kv.Value );
			}

			// Add the new base resistances.
			foreach ( var kv in item.Resistances )
				AddResistance( kv.Key, kv.Value );

			if ( item.UsePathfinder )
				Pathfinder = PathManager.GetPathfinder( item.NodeSize, item.CollisionSize );
			else
				Pathfinder = PathManager.Default;

			if ( item.UseModelPhysics )
				SetupPhysicsFromModel( PhysicsMotionType.Keyframed );
			else
				SetupPhysicsFromCapsule( PhysicsMotionType.Keyframed, Capsule.FromHeightAndRadius( 72, item.NodeSize * 0.5f ) );

			LocalCenter = CollisionBounds.Center;
			AgentRadius = GetDiameterXY( Item.AgentRadiusScale );

			IdleLoopSound.Stop();

			if ( !string.IsNullOrEmpty( item.IdleLoopSound ) )
				IdleLoopSound = PlaySound( item.IdleLoopSound );

			if ( Weapon.IsValid() ) Weapon.Delete();

			if ( !string.IsNullOrEmpty( item.Weapon ) )
			{
				ChangeWeapon( item.Weapon );
			}

			Position = Position.WithZ( GetVerticalOffset() );

			base.OnItemChanged( item, oldItem );
		}

		public void ChangeWeapon( string name )
		{
			if ( Weapon.IsValid() )
			{
				Weapon.Delete();
				Weapon = null;
			}

			Weapon = TypeLibrary.Create<Weapon>( name );
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

		protected virtual void OnMoveStackEmpty()
		{
			if ( InternalTargetInfo.Type == UnitTargetType.Gather
				|| InternalTargetInfo.Type == UnitTargetType.Deposit )
			{
				FindTargetResource();
			}
		}

		protected virtual void CreateModifiers()
		{
			Modifiers = new UnitModifiers();
		}

		[Event.Client.Frame]
		protected virtual void ClientFrame()
		{
			if ( Hud.Style.Opacity != RenderColor.a )
			{
				Hud.Style.Opacity = RenderColor.a;
				Hud.Style.Dirty();
			}

			Hud.SetActive( RenderColor.a > 0f );
		}

		protected override void ClientTick()
		{
			base.ClientTick();

			UpdatePathParticles();

			if ( Occupiable.IsValid() )
			{
				Circle.Alpha = 0f;
				RenderColor = RenderColor.WithAlpha( 0f );

				return;
			}

			if ( Circle.IsValid() && Player.IsValid() )
			{
				if ( IsLocalPlayers && IsSelected )
					Circle.Color = Color.White;
				else
					Circle.Color = Player.TeamColor;

				Circle.Alpha = 1f;
			}

			if ( IsLocalTeamGroup )
			{
				var isOnScreen = IsOnScreen();

				Circle.Alpha = isOnScreen ? 1f : 0f;
				RenderColor = RenderColor.WithAlpha( isOnScreen ? 1f : 0f );
				
				return;
			}

			RenderColor = RenderColor.WithAlpha( RenderColor.a.LerpTo( TargetAlpha, Time.Delta * 2f ) );

			for ( var i = 0; i < Children.Count; i++ )
			{
				if ( Children[i] is ModelEntity child )
				{
					child.RenderColor = child.RenderColor.WithAlpha( RenderColor.a );
				}
			}

			if ( Circle.IsValid() )
			{
				Circle.Alpha = RenderColor.a;
			}
		}

		[ClientRpc]
		protected virtual void CreateDeathParticles()
		{
			if ( !string.IsNullOrEmpty( Item.DeathParticles ) )
			{
				var particles = Particles.Create( Item.DeathParticles );
				particles.SetPosition( 0, Position );
			}
		}

		protected virtual void UpdatePathParticles()
		{
			if ( !IsLocalPlayers || !IsSelected || Destination.IsNearZeroLength || Velocity.IsNearZeroLength )
			{
				RemovePathParticles();
				return;
			}
			
			if ( PathParticles == null )
			{
				CreatePathParticles();
			}

			PathParticles.SetPosition( 1, GetPathDestination().WithZ( Position.z ) );
		}

		protected virtual void OnTargetChanged()
		{
			if ( Weapon.IsValid() )
			{
				Weapon.Target = InternalTargetInfo.Entity;
				Weapon.Occupiable = Occupiable;
			}

			if ( IsSelected )
			{
				CreatePathParticles( To.Single( Player ) );
			}

			TargetEntity = InternalTargetInfo.Entity;
			SpinSpeed = null;
		}

		protected override void AddHudComponents()
		{
			RankIcon = Hud.AddChild<EntityHudIcon>( "rank" );
			HealthBar = Hud.AddChild<EntityHudBar>( "health" );

			if ( IsLocalPlayers )
				GatherBar = Hud.AddChild<EntityHudBar>( "gather" );

			if ( Item.Occupiable.Enabled )
				OccupantsHud = Hud.AddChild<EntityHudIconList>();

			base.AddHudComponents();
		}

		[ClientRpc]
		private void BecomeRagdoll( Vector3 velocity, bool isBullet, bool isBlast, bool isPhysicsImpact, Vector3 forcePos, Vector3 force, int bone )
		{
			Ragdoll.From( this, velocity, isBullet, isBlast, isPhysicsImpact, forcePos, force, bone ).FadeOut( 10f );
		}

		private void TryFinishMoveGroup()
		{
			if ( MoveStack.TryPeek( out var group ) )
			{
				Destination = group.GetDestination();
				group.TryFinish( this );
			}
			else
			{
				Destination = Vector3.Zero;
			}
		}

		private void OnKillsChanged()
		{
			UpdateRank( Ranks.Find( Kills ) );
		}

		private void ResetTarget()
		{
			InternalTargetInfo.Entity = null;
			InternalTargetInfo.Position = null;
			InternalTargetInfo.Follow = false;
			InternalTargetInfo.Type = UnitTargetType.None;

			IsGathering = false;
		}

		private void FindTargetResource()
		{
			GatherCommand command;

			if ( MoveStack.TryPeek( out var group ) )
			{
				// Try to find a resource depo before we move on with our queue.
				if ( !FindResourceDepo() )
				{
					group.Resume( this );
				}

				return;
			}

			// If our last resource entity is valid just use that.
			if ( InternalGatherInfo.Entity.IsValid() )
			{
				command = new GatherCommand
				{
					Target = InternalGatherInfo.Entity
				};

				PushMoveGroup( GetDestinations( InternalGatherInfo.Entity ), command );

				return;
			}

			var entities = Entity.FindInSphere( InternalGatherInfo.Position, 2000f );

			foreach ( var entity in entities )
			{
				if ( entity is ResourceEntity resource && resource.Resource == InternalGatherInfo.Type )
				{
					command = new GatherCommand
					{
						Target = resource
					};

					PushMoveGroup( GetDestinations( resource ), command );

					return;
				}
			}
		}

		private bool FindResourceDepo()
		{
			var buildings = Player.GetBuildings().Where( i => i.CanDepositResources );
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
			{
				var command = new DepositCommand
				{
					Target = closestDepo
				};

				PushMoveGroup( GetDestinations( closestDepo ), command );

				return true;
			}

			return false;
		}

		private void FindTargetEnemy()
		{
			var searchPosition = Position;
			var searchRadius = Item.AttackRadius;

			if ( Occupiable is IOccupiableEntity occupiable )
			{
				var attackRadius = occupiable.GetAttackRadius();

				if ( attackRadius > 0f )
					searchRadius = attackRadius;

				searchPosition = occupiable.Position;
			}

			var entities = Entity.FindInSphere( searchPosition.WithZ( 0f ), searchRadius * 1.2f );

			TargetBuffer.Clear();

			foreach ( var entity in entities )
			{
				if ( entity is ISelectable selectable && CanAttackTarget( selectable ) )
				{
					TargetBuffer.Add( selectable );
				}
			}

			TargetBuffer.OrderByDescending( s => s.GetAttackPriority() )
				.ThenBy( s => s.Position.Distance( searchPosition ) );

			if ( TargetBuffer.Count > 0 )
			{
				SetAttackTarget( TargetBuffer[0], false );
			}
		}

		private void TickFindTarget()
		{
			if ( IsMoveGroupValid() || !Weapon.IsValid() )
				return;

			if ( InternalTargetInfo.Follow )
				return;

			if ( NextFindTarget )
			{
				FindTargetEnemy();
				NextFindTarget = 1f;
			}
		}

		private void TickOccupantAttack()
		{
			if ( Occupiable is not IOccupiableEntity occupiable )
				return;

			if ( occupiable.CanOccupantsAttack() && IsTargetValid() )
			{
				if ( IsTargetInRange() && InternalTargetInfo.Type == UnitTargetType.Attack )
				{
					if ( Weapon.IsValid() && Weapon.CanAttack() )
					{
						Weapon.Attack();
					}
				}
			}

			TickFindTarget();
		}

		private void TickInteractWithTarget()
		{
			var lookAtDistance = 0f;

			if ( !SpinSpeed.HasValue )
				lookAtDistance = LookAtEntity( InternalTargetInfo.Entity, Time.Delta * Item.RotateToTargetSpeed );
			else
				Rotation = Rotation.FromYaw( Rotation.Yaw() + SpinSpeed.Value * Time.Delta );

			if ( SpinSpeed.HasValue || lookAtDistance.AlmostEqual( 0f, 0.1f ) )
			{
				if ( InternalTargetInfo.Type == UnitTargetType.Occupy )
				{
					if ( InternalTargetInfo.Entity is IOccupiableEntity occupiable && occupiable.Player == Player )
					{
						if ( occupiable.CanOccupyUnits )
						{
							TickOccupy( occupiable );
							return;
						}
					}
				}

				if ( InternalTargetInfo.Type == UnitTargetType.Construct )
				{
					if ( InternalTargetInfo.Entity is BuildingEntity building && building.Player == Player )
					{
						if ( building.IsUnderConstruction )
							TickConstruct( building );

						return;
					}
				}

				if ( InternalTargetInfo.Type == UnitTargetType.Repair )
				{
					if ( InternalTargetInfo.Entity is BuildingEntity building && building.Player == Player )
					{
						if ( !building.IsUnderConstruction && building.IsDamaged() )
						{
							TickRepair( building );
							return;
						}
					}
				}

				if ( InternalTargetInfo.Type == UnitTargetType.Deposit )
				{
					if ( InternalTargetInfo.Entity is BuildingEntity building && building.Player == Player )
					{
						if ( building.CanDepositResources )
						{
							DepositResources();
							return;
						}
					}
				}

				if ( InternalTargetInfo.Type == UnitTargetType.Gather )
				{
					if ( InternalTargetInfo.Entity is ResourceEntity resource )
					{
						if ( SpinSpeed.HasValue || lookAtDistance.AlmostEqual( 0f, 0.1f ) )
						{
							TickGather( resource );
							return;
						}
					}
				}

				if ( InternalTargetInfo.Type == UnitTargetType.Attack )
				{
					if ( Weapon.IsValid() && Weapon.CanAttack() )
					{
						if ( lookAtDistance.AlmostEqual( 0f, Weapon.RotationTolerance ) )
						{
							Weapon.Attack();
							return;
						}
					}
				}
			}
		}

		private bool ValidateAttackDistance()
		{
			if ( !InternalTargetInfo.HasEntity() ) return false;

			var minAttackDistance = Item.MinAttackDistance;
			var target = InternalTargetInfo.Entity;

			if ( minAttackDistance == 0f ) return true;

			var tolerance = (Pathfinder.NodeSize * 2f);
			var targetPosition = target.Position.WithZ( 0f );
			var selfPosition = Position.WithZ( 0f );

			if ( targetPosition.Distance( selfPosition ) >= minAttackDistance - tolerance )
				return true;

			var position = GetPerimeterPosition( targetPosition, minAttackDistance );

			if ( !position.HasValue )
				return true;

			PushMoveGroup( position.Value );

			return false;
		}

		private void TickAbility()
		{
			var ability = UsingAbility;

			if ( ability == null || !ability.LookAtTarget )
				return;

			LookAtPosition( ability.TargetInfo.Origin, Time.Delta * Item.RotateToTargetSpeed );
		}

		private void UpdateFlockBuffer()
		{
			var bufferIndex = 0;
			var neighbours = Entity.FindInSphere( Position, AgentRadius * 0.35f );

			foreach ( var neighbour in neighbours )
			{
				if ( neighbour is UnitEntity unit && ShouldOtherUnitFlock( unit ) )
				{
					BlockBuffer[bufferIndex] = unit;

					bufferIndex++;

					if ( bufferIndex >= BlockBuffer.Length )
						break;
				}
			}

			if ( bufferIndex < 8 )
			{
				Array.Clear( BlockBuffer, bufferIndex, BlockBuffer.Length - bufferIndex );
			}
		}

		private void UpdateFollowPosition( bool isTargetValid )
		{
			if ( !isTargetValid || !InternalTargetInfo.Follow )
				return;

			InternalTargetInfo.Position = InternalTargetInfo.Entity.Position;
		}

		private void TickMoveToTarget( bool isTargetValid, UnitAnimator animator )
		{
			UpdateFollowPosition( isTargetValid );

			var nodeDirection = Vector3.Zero;
			var movementSpeed = GetSpeed();
			var direction = Vector3.Zero;

			if ( MoveStack.TryPeek( out var group ) )
			{
				var node = Pathfinder.CreateWorldPosition( Position );
				Pathfinder.DrawBox( node, Color.Green );

				if ( !IsAtDestination() )
				{
					if ( Item.UsePathfinder )
					{
						var offset = Pathfinder.CenterOffset.Normal;
						direction = group.GetDirection( Position );
						nodeDirection = (direction.Normal * offset).WithZ( 0f );
					}
					else
					{
						direction = (group.GetDestination() - Position).Normal;
						nodeDirection = direction.WithZ( 0f );
					}
				}
			}
			else if ( InternalTargetInfo.Position.HasValue )
			{
				var straightDirection = (InternalTargetInfo.Position.Value - Position).Normal.WithZ( 0f );

				if ( Pathfinder.IsAvailable( Position + (straightDirection * Pathfinder.NodeSize) ) )
					nodeDirection = straightDirection;
				else
					nodeDirection = Vector3.Zero;
			}

			if ( IsSlowTick() )
			{
				UpdateFlockBuffer();
			}

			var flocker = new Flocker();
			flocker.Setup( this, BlockBuffer, Position, movementSpeed );
			flocker.Flock( Position + direction * Math.Max( AgentRadius, Pathfinder.NodeSize ) );
			var steerDirection = flocker.Force.WithZ( 0f );

			if ( steerDirection.Length > 0 )
			{
				if ( !Item.UsePathfinder || Pathfinder.IsAvailable( Position + (steerDirection.Normal * Pathfinder.NodeSize) ) )
				{
					Velocity = steerDirection.ClampLength( movementSpeed );
				}
			}

			var acceleration = 4f;

			if ( Velocity.Length == 0 )
			{
				acceleration = 16f;
				Velocity = (nodeDirection * movementSpeed);
			}
			
			TargetVelocity = TargetVelocity.LerpTo( Velocity, Time.Delta * acceleration );
			Position += TargetVelocity * Time.Delta;

			var verticalOffset = GetVerticalOffset();
			Position = Position.LerpTo( Position.WithZ( verticalOffset ), Time.Delta * GetVerticalSpeed() );

			if ( Item.AlignToSurface )
			{
				var normal = GetGroundNormal();
				var targetRotation = Rotation.LookAt( normal, Rotation.Forward );
				targetRotation = targetRotation.RotateAroundAxis( Vector3.Left, 90f );
				targetRotation = targetRotation.RotateAroundAxis( Vector3.Up, 180f );
				Rotation = Rotation.Lerp( Rotation, targetRotation, Time.Delta * movementSpeed / 20f );
			}

			var walkVelocity = Velocity.WithZ( 0 );

			if ( walkVelocity.Length > 1 )
			{
				if ( animator != null )
				{
					if ( movementSpeed >= 300f )
						animator.Speed = 1f;
					else
						animator.Speed = 0.5f;
				}

				Rotation = Rotation.Lerp( Rotation, Rotation.LookAt( walkVelocity.Normal, Vector3.Up ), Time.Delta * 4f );
			}
		}

		private bool ShouldOtherUnitFlock( UnitEntity unit )
		{
			if ( unit.Velocity.Length > 0 )
				return true;

			if ( unit.TargetType == UnitTargetType.Gather )
				return false;
			else if ( unit.TargetType == UnitTargetType.Construct )
				return false;
			else if ( unit.TargetType == UnitTargetType.Repair )
				return false;

			return true;
		}

		private void TickOccupy( IOccupiableEntity occupiable )
		{
			if ( occupiable.OccupyUnit( this ) )
				ClearTarget();
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

		private void TickRepair( BuildingEntity building )
		{
			var repairAmount = building.MaxHealth / building.Item.BuildTime * 0.5f;
			var fraction = repairAmount / building.MaxHealth;
			var repairCosts = new Dictionary<ResourceType, int>();

			foreach ( var kv in building.Item.Costs )
			{
				repairCosts[kv.Key] = (kv.Value * fraction).CeilToInt();
			}

			if ( !Player.CanAfford( repairCosts ) )
			{
				LookAtEntity( building );
				SpinSpeed = 0f;
				return;
			}

			SpinSpeed = (building.MaxHealth / building.Health) * 200f;

			if ( !NextRepairTime ) return;

			Player.TakeResources( repairCosts );

			ResourceHint.Send( Player, 0.5f, Position, repairCosts, Color.Red );

			building.Health += repairAmount;
			building.Health = building.Health.Clamp( 0f, building.Item.MaxHealth );

			if ( building.Health == building.Item.MaxHealth )
			{
				LookAtEntity( building );
				building.FinishRepair();
			}
			else
			{
				building.UpdateRepair();
			}

			NextRepairTime = 1f;
		}

		private void TickConstruct( BuildingEntity building )
		{
			if ( building.TouchingEntities.Count > 0 )
			{
				var blueprints = building.TouchingEntities
					.OfType<BuildingEntity>()
					.Where( v => v.IsBlueprint );

				foreach ( var blueprint in blueprints )
				{
					blueprint.CancelConstruction();
				}

				SpinSpeed = 0;

				return;
			}

			var itemNetworkId = building.ItemNetworkId;

			// Check if we can build this instantly.
			if ( !Player.InstantBuildCache.Contains( itemNetworkId ) )
			{
				if ( building.Item.BuildFirstInstantly )
				{
					Player.InstantBuildCache.Add( itemNetworkId );

					LookAtEntity( building );
					building.FinishConstruction();

					return;
				}
			}

			var numberOfConstructors = building.GetActiveConstructorCount();
			if ( numberOfConstructors < 1 ) return;

			var buildTime = building.Item.BuildTime * (3f / (numberOfConstructors + 2f));
			var buildDelta = (building.MaxHealth / buildTime) / numberOfConstructors;

			building.Health += buildDelta * Time.Delta;
			building.Health = building.Health.Clamp( 0f, building.Item.MaxHealth );

			SpinSpeed = (building.MaxHealth / building.Health) * 200f;

			if ( building.Health == building.Item.MaxHealth )
			{
				LookAtEntity( building );
				building.FinishConstruction();
			}
			else
			{
				building.UpdateConstruction();
			}
		}

		private void TickGather( ResourceEntity resource )
		{
			if ( InternalGatherInfo.LastGather < resource.GatherTime )
				return;

			InternalGatherInfo.LastGather = 0;
			IsGathering = true;

			TakeFrom( resource );

			if ( Carrying.TryGetValue( resource.Resource, out var carrying ) )
			{
				var maxCarry = resource.MaxCarry * Item.MaxCarryMultiplier;

				GatherProgress = (1f / maxCarry) * carrying;
				if ( carrying < maxCarry ) return;

				FindResourceDepo();
			}
		}
	}
}

