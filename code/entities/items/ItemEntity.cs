﻿using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using Gamelib.Extensions;
using Gamelib.FlowFields.Grid;
using Facepunch.RTS.Tech;
using Facepunch.RTS.Upgrades;
using System.IO;
using Sandbox.Component;

namespace Facepunch.RTS
{
	public abstract partial class ItemEntity<T> : AnimatedEntity, ISelectable, IHudEntity, ITooltipEntity where T : BaseItem
	{
		public virtual bool CanMultiSelect => false;
		public virtual bool HasSelectionGlow => true;

		public Dictionary<string, BaseAbility> AbilityTable { get; private set; }
		public Dictionary<string, IStatus> StatusTable { get; private set; }
		public Dictionary<string, ItemComponent> ItemComponents { get; private set; }
		public BaseAbility UsingAbility { get; private set; }
		[Net, Change] public uint ItemNetworkId { get; private set; }
		[Net] public IList<uint> Upgrades { get; private set; }
		[Net] public RTSPlayer Player { get; private set; }
		[Net] public float MaxHealth { get; set; }
		public EntityHudAnchor Hud { get; private set; }
		public EntityHudIcon StatusIcon { get; private set; }
		public EntityHudIconBar QueueHud { get; private set; }
		public Vector3 LocalCenter { get; protected set; }
		public List<QueueItem> Queue { get; private set; }
		public uint LastQueueId { get; private set; }

		Sandbox.Internal.EntityTags ISelectable.Tags => Tags;

		public string ItemId => Item.UniqueId;
		public bool IsSelected => Tags.Has( "selected" );
		public bool IsLocalPlayers => Player.IsValid() && Game.LocalPawn == Player;
		public bool IsLocalTeamGroup => Player.IsValid() && (Game.LocalPawn as RTSPlayer).TeamGroup == Player.TeamGroup;

		private T ItemCache;

		public T Item
		{
			get
			{
				if ( ItemCache == null )
					ItemCache = Items.Find<T>( ItemNetworkId );
				return ItemCache;
			}
		}

		public ItemEntity()
		{
			Transmit = TransmitType.Always;
			Upgrades = new List<uint>();
			StatusTable = new();
			ItemComponents = new();
			Queue = new List<QueueItem>();
		}

		public BaseItem GetBaseItem() => Item;

		public void QueueItem( BaseItem item )
		{
			Game.AssertServer();

			LastQueueId++;

			var queueItem = new QueueItem
			{
				Item = item,
				Id = LastQueueId
			};

			if ( Player.SkipAllWaiting )
			{
				OnQueueItemCompleted( queueItem );
				queueItem.Item.OnCreated( Player, this );
				RefreshSelection( To.Single( Player ) );
				return;
			}

			Queue.Add( queueItem );

			AddToQueue( To.Single( Player ), LastQueueId, item.NetworkId );

			if ( Queue.Count == 1 )
			{
				queueItem.Start();
				StartQueueItem( To.Single( Player ), LastQueueId, queueItem.FinishTime );
			}
		}

		public bool IsSlowTick()
		{
			return (Time.Tick % 3 == 0);
		}

		public BaseItem UnqueueItem( uint queueId )
		{
			Game.AssertServer();

			BaseItem removedItem = default;

			for ( var i = Queue.Count - 1; i >= 0; i-- )
			{
				if ( Queue[i].Id == queueId )
				{
					removedItem = Queue[i].Item;
					Queue.RemoveAt( i );
					break;
				}
			}

			RemoveFromQueue( To.Single( Player ), queueId );

			if ( Queue.Count > 0 )
			{
				var firstItem = Queue[0];

				if ( firstItem.FinishTime == 0f )
				{
					firstItem.Start();
					StartQueueItem( To.Single( Player ), firstItem.Id, firstItem.FinishTime );
				}
			}

			return removedItem;
		}

		public bool IsOnScreen()
		{
			var position = Position.ToScreen();
			return position.x > 0f && position.y > 0f && position.x < 1f && position.y < 1f;
		}

		public bool IsInQueue( BaseItem item )
		{
			for ( var i = 0; i < Queue.Count; i++)
			{
				if ( Queue[i].Item == item )
					return true;
			}

			return false;
		}

		public IEnumerable<BaseUpgrade> GetUpgrades()
		{
			for ( var i = 0; i < Upgrades.Count; i++ )
			{
				var networkId = Upgrades[i];
				yield return Items.Find<BaseUpgrade>( networkId );
			}
		}

		public bool HasUpgrade( BaseUpgrade item )
		{
			return Upgrades.Contains( item.NetworkId );
		}

		public bool HasUpgrade( uint id )
		{
			return Upgrades.Contains( id );
		}

		public bool HasComponent<C>() where C : ItemComponent
		{
			var componentName = TypeLibrary.GetType<C>().ClassName;
			return ItemComponents.ContainsKey( componentName );
		}

		public C GetComponent<C>() where C : ItemComponent
		{
			var componentName = TypeLibrary.GetType<C>().ClassName;

			if ( ItemComponents.TryGetValue( componentName, out var component ) )
				return (component as C);

			return null;
		}

		public void RemoveComponent<C>() where C : ItemComponent
		{
			var componentName = TypeLibrary.GetType<C>().ClassName;

			if ( ItemComponents.ContainsKey( componentName ) )
			{
				ItemComponents.Remove( componentName );
			}
		}

		public C AddComponent<C>() where  C : ItemComponent
		{
			var component = GetComponent<C>();
			if ( component != null ) return component;

			var componentName = TypeLibrary.GetType<C>().ClassName;
			component = TypeLibrary.Create<C>( componentName );
			ItemComponents.Add( componentName, component );

			return component;
;		}

		public BaseAbility GetAbility( string id )
		{
			if ( AbilityTable.TryGetValue( id, out var ability ) )
				return ability;

			return null;
		}

		public bool HasStatus( string id )
		{
			return StatusTable.ContainsKey( id );
		}

		public bool HasStatus<S>() where S : IStatus
		{
			var id = TypeLibrary.GetType<S>().ClassName;
			return HasStatus( id );
		}

		public S ApplyStatus<S>( StatusData data ) where S : IStatus
		{
			Game.AssertServer();

			using var stream = new MemoryStream();
			using var writer = new BinaryWriter( stream );

			data.Serialize( writer );

			var id = TypeLibrary.GetType<S>().ClassName;

			ClientApplyStatus( To.Everyone, id, stream.GetBuffer() );

			if ( StatusTable.TryGetValue( id, out var status ) )
			{
				status.SetData( data );
				status.Restart();

				return (S)status;
			}

			status = Statuses.Create( id );

			StatusTable.Add( id, status );

			status.SetData( data );
			status.Initialize( id, this );
			status.OnApplied();

			return (S)status;
		}

		public void RemoveAllStatuses()
		{
			if ( Game.IsServer ) ClientRemoveAllStatuses( To.Everyone );

			foreach ( var kv in StatusTable )
			{
				kv.Value.OnRemoved();
			}

			StatusTable.Clear();
		}

		public bool IsSameTeamGroup( ISelectable other )
		{
			return (other.Player.TeamGroup == Player.TeamGroup);
		}

		public void RemoveStatus( string id )
		{
			if ( StatusTable.TryGetValue( id, out var status ) )
			{
				if ( Game.IsServer )
					ClientRemoveStatus( To.Everyone, id );

				StatusTable.Remove( id );
				status.OnRemoved();
			}
		}

		public bool IsUsingAbility()
		{
			return (UsingAbility != null);
		}

		public virtual int GetAttackPriority()
		{
			return 0;
		}

		public virtual void ShowTooltip()
		{

		}

		public virtual void StartAbility( BaseAbility ability, AbilityTargetInfo info )
		{
			CancelAbility();

			ability.LastUsedTime = 0;
			ability.NextUseTime = ability.Cooldown;
			ability.TargetInfo = info;

			ability.OnStarted();

			if ( Game.IsServer )
			{
				ClientStartAbility( To.Single( Player ), ability.UniqueId, (Entity)info.Target, info.Origin );
			}

			UsingAbility = ability;

			if ( ability.Duration == 0f )
			{
				FinishAbility();
			}
		}

		public virtual void FinishAbility()
		{
			if ( UsingAbility != null )
			{
				UsingAbility.OnFinished();
				UsingAbility = null;

				if ( Game.IsServer )
				{
					ClientFinishAbility( To.Single( Player ) );
				}
			}
		}

		public virtual void CancelAbility()
		{
			if ( UsingAbility != null )
			{
				UsingAbility.OnCancelled();
				UsingAbility = null;

				if ( Game.IsServer )
				{
					ClientCancelAbility( To.Single( Player ) );
				}
			}
		}

		public bool IsEnemy( ISelectable other )
		{
			return !IsSameTeamGroup( other );
		}

		public Vector3 GetFreePosition( UnitEntity unit, float diameterScale = 0.75f )
		{
			var bounds = GetDiameterXY( diameterScale );
			var pathfinder = unit.Pathfinder;
			var potentialNodes = new List<GridWorldPosition>();
			
			pathfinder.GetGridPositions( Position, bounds, potentialNodes, true );

			var freeLocations = potentialNodes.ToList();

			if ( freeLocations.Count == 0 )
			{
				throw new Exception( "[ItemEntity::PlaceNear] Unable to find a free location to spawn the unit!" );
			}

			var randomLocation = freeLocations[Game.Random.Int( freeLocations.Count - 1 )];
			
			return pathfinder.GetPosition( randomLocation ) + new Vector3( 0f, 0f, pathfinder.GetHeight( randomLocation ) );
		}

		public void PlaceNear( UnitEntity unit, float diameterScale = 0.75f )
		{
			unit.Position = GetFreePosition( unit, diameterScale );
		}

		public bool IsInRange( Entity entity, float radius, float tolerance = 1f )
		{
			var targetPosition = entity.Position.WithZ( 0f );
			var selfPosition = Position.WithZ( 0f );

			if ( entity is ModelEntity modelEntity )
			{
				// We can try to see if our range overlaps the bounding box of the target.
				var targetBounds = (modelEntity.CollisionBounds * tolerance) + targetPosition.WithZ( 0f );

				if ( targetBounds.Overlaps( selfPosition, radius ) )
					return true;
			}

			return (targetPosition.Distance( selfPosition ) < radius);
		}

		public void Assign( RTSPlayer player, T item )
		{
			Game.AssertServer();

			var oldItem = Item;

			Player = player;
			ItemNetworkId = item.NetworkId;

			ClearItemCache();
			OnItemChanged( item, oldItem );
			OnPlayerAssigned( player );
		}

		public void ChangeTo( T item )
		{
			Game.AssertServer();

			var oldItem = Item;

			ItemNetworkId = item.NetworkId;
			ClearItemCache();
			OnItemChanged( item, oldItem );

			if ( IsSelected )
			{
				ForceClientReselect( To.Single( Player ) );
			}
		}

		public float GetDiameterXY( float scalar = 1f, bool smallestSide = false )
		{
			return EntityExtension.GetDiameterXY( this, scalar, smallestSide );
		}

		public void ClearItemCache() => ItemCache = null;

		public void Assign( RTSPlayer player, string itemId )
		{
			Game.AssertServer();

			var item = Items.Find<T>( itemId );

			Assign( player, item );
		}

		public virtual bool CanBeAttacked()
		{
			return true;
		}

		public virtual void CancelAction() { }

		public virtual void Select()
		{
			if ( Player.IsValid() )
			{
				Player.Selection.Add( this );
				Tags.Add( "selected" );
			}
		}

		public virtual void Deselect()
		{
			if ( Player.IsValid() )
			{
				Player.Selection.Remove( this );
				Tags.Remove( "selected" );
			}
		}

		public virtual bool CanSelect()
		{
			return true;
		}

		public virtual bool ShouldUpdateHud()
		{
			return EnableDrawing && Hud.IsActive;
		}

		public virtual void UpdateHudComponents()
		{
			var status = StatusTable.FirstOrDefault();

			if ( status.Value != null && status.Value.Icon != null )
			{
				StatusIcon.Texture = status.Value.Icon;
				StatusIcon.SetClass( "hidden", false );
			}
			else
			{
				StatusIcon.SetClass( "hidden", true );
			}

			if ( QueueHud != null && Queue.Count > 0 )
			{
				var queueItem = Queue[0];

				QueueHud.Icon.Texture = queueItem.Item.Icon;
				QueueHud.Bar.SetProgress( 1f - (queueItem.GetTimeLeft() / queueItem.Item.BuildTime) );
				QueueHud.SetActive( true );
			}
			else
			{
				QueueHud?.SetActive( false );
			}
		}

		public override void TakeDamage( DamageInfo info )
		{
			Player.WarnUnderAttack( this );

			foreach ( var component in ItemComponents.Values )
				info = component.TakeDamage( info );

			base.TakeDamage( info );
		}

		public override void ClientSpawn()
		{
			Hud = EntityHud.Instance.Create( this );
			Hud.SetActive( true );

			AddHudComponents();

			base.ClientSpawn();
		}

		[Event.Tick]
		protected virtual void Tick()
		{
			if ( UsingAbility != null )
			{
				UsingAbility.Tick();
			}

			foreach ( var kv in StatusTable )
			{
				var status = kv.Value;

				if ( status.EndTime )
					RemoveStatus( status.UniqueId );
				else
					status.Tick();
			}
		}

		[Event.Tick.Server]
		protected virtual void ServerTick()
		{
			if ( Queue.Count > 0 )
			{
				var firstItem = Queue[0];

				if ( firstItem.FinishTime > 0f && RTSGame.Entity.ServerTime >= firstItem.FinishTime )
				{
					OnQueueItemCompleted( firstItem );
					UnqueueItem( firstItem.Id );
					firstItem.Item.OnCreated( Player, this );
				}
			}

			var ability = UsingAbility;

			if ( ability != null && ability.LastUsedTime >= ability.Duration )
			{
				FinishAbility();
			}
		}

		[ClientRpc]
		protected virtual void ForceClientReselect()
		{
			if ( IsLocalPlayers && IsSelected )
			{
				SelectedItem.Instance.Update( Player.Selection );
				OnSelected();
			}
		}

		[Event.Tick.Client]
		protected virtual void ClientTick()
		{

		}

		protected virtual void OnQueueItemCompleted( QueueItem queueItem )
		{
			if ( queueItem.Item is BaseTech tech )
			{
				Player.AddDependency( tech );
				return;
			}

			if ( queueItem.Item is BaseUpgrade upgrade )
			{
				var changeItemTo = upgrade.ChangeItemTo;

				if ( !string.IsNullOrEmpty( changeItemTo ) )
					ChangeTo( Items.Find<T>( changeItemTo ) );

				Upgrades.Add( upgrade.NetworkId );
			}
		}

		protected virtual void OnItemNetworkIdChanged()
		{
			ClearItemCache();
			CreateAbilities();
		}

		protected virtual void AddHudComponents()
		{
			StatusIcon = Hud.AddChild<EntityHudIcon>( "status" );

			if ( IsLocalPlayers )
				QueueHud = Hud.AddChild<EntityHudIconBar>();
		}

		protected override void OnDestroy()
		{
			if ( Game.IsServer )
			{
				RemoveAllStatuses();
				CancelAbility();
				Deselect();
			}

			if ( Game.IsClient ) Hud.Delete();

			base.OnDestroy();
		}

		protected override void OnTagAdded( string tag )
		{
			if ( IsLocalPlayers && tag.ToLower() == "selected" )
			{
				OnSelected();
			}

			base.OnTagAdded( tag );
		}

		protected override void OnTagRemoved( string tag )
		{
			if ( IsLocalPlayers && tag.ToLower() == "selected" )
			{
				OnDeselected();
			}

			base.OnTagRemoved( tag );
		}

		protected virtual void OnSelected()
		{
			if ( HasSelectionGlow )
			{
				var glow = Components.GetOrCreate<Glow>();
				glow.Enabled = true;
				glow.Color = Player.TeamColor.WithAlpha( 0.5f );
			}
		}

		protected virtual void OnDeselected()
		{
			if ( HasSelectionGlow )
			{
				var glow = Components.GetOrCreate<Glow>();
				glow.Enabled = false;
			}
		}

		protected virtual void OnPlayerAssigned( RTSPlayer player) { }

		protected virtual void OnItemChanged( T item, T oldItem )
		{
			if ( oldItem != null )
			{
				foreach ( var tag in oldItem.Tags )
					Tags.Remove( tag );
			}

			foreach ( var tag in item.Tags )
				Tags.Add( tag );

			CreateAbilities();
		}

		protected virtual void CreateAbilities()
		{
			AbilityTable = new();

			foreach ( var id in Item.Abilities )
			{
				var ability = Abilities.Create( id );
				ability.Initialize( id, this );
				AbilityTable[id] = ability;
			}
		}

		[ClientRpc]
		private void StartQueueItem( uint queueId, float finishTime )
		{
			for ( var i = Queue.Count - 1; i >= 0; i-- )
			{
				if ( Queue[i].Id == queueId )
				{
					Queue[i].FinishTime = finishTime;
					return;
				}
			}
		}

		[ClientRpc]
		private void RemoveFromQueue( uint queueId )
		{
			for ( var i = Queue.Count - 1; i >= 0; i-- )
			{
				if ( Queue[i].Id == queueId )
				{
					Queue.RemoveAt( i );
					RefreshSelection();
					return;
				}
			}
		}

		[ClientRpc]
		private void AddToQueue( uint queueId, uint itemId )
		{
			var queueItem = new QueueItem
			{
				Item = Items.Find<BaseItem>( itemId ),
				Id = queueId
			};

			Queue.Add( queueItem );

			RefreshSelection();
		}

		[ClientRpc]
		private void ClientRemoveAllStatuses()
		{
			RemoveAllStatuses();
		}

		[ClientRpc]
		private void ClientApplyStatus( string id, byte[] data )
		{
			using var stream = new MemoryStream( data );
			using var reader = new BinaryReader( stream );

			if ( StatusTable.TryGetValue( id, out var status ) )
			{
				status.Deserialize( reader );
				status.Restart();
				return;
			}

			status = Statuses.Create( id );

			StatusTable.Add( id, status );

			status.Deserialize( reader );
			status.Initialize( id, this );
			status.OnApplied();
		}

		[ClientRpc]
		private void ClientRemoveStatus( string id )
		{
			RemoveStatus( id );
		}

		[ClientRpc]
		private void ClientStartAbility( string id, Entity target, Vector3 origin )
		{
			StartAbility( GetAbility( id ), new AbilityTargetInfo()
			{
				Target = target as ISelectable,
				Origin = origin
			} );
			
			RefreshSelection();
		}

		[ClientRpc]
		private void ClientFinishAbility()
		{
			FinishAbility();
			RefreshSelection();
		}

		[ClientRpc]
		private void ClientCancelAbility()
		{
			CancelAbility();
			RefreshSelection();
		}

		[ClientRpc]
		private void RefreshSelection()
		{
			if ( !IsLocalPlayers || !IsSelected ) return;
			SelectedItem.Instance.Update( Player.Selection );
		}
	}
}

