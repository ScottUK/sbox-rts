﻿using Facepunch.RTS.Buildings;
using Facepunch.RTS.Tech;
using Facepunch.RTS.Units;
using Facepunch.RTS.Upgrades;
using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;
using System.Collections.Generic;
using System.Linq;

namespace Facepunch.RTS
{
	public class ItemCommandAbility : ItemCommand
	{
		public BaseAbility Ability { get; private set; }
		public Panel Countdown { get; private set; }

		public ItemCommandAbility() : base()
		{
			Countdown = Add.Panel( "countdown" );
		}

		protected override void OnClick( MousePanelEvent e )
		{
			if ( IsDisabled ) return;

			if ( Selectable.IsUsingAbility() )
			{
				// We can't use another while once is being used.
				Audio.Play( "rts.beepvibrato" );
				return;
			}
			
			var status = Ability.CanUse();

			if ( status != RequirementError.Success )
			{
				Audio.Play( status );
				return;
			}

			if ( Ability.TargetType == AbilityTargetType.Self )
				Abilities.UseOnSelf( Selectable.NetworkIdent, Ability.UniqueId );
			else
				Abilities.SelectTarget( Ability );

			Audio.Play( "rts.pophappy" );
		}

		protected override void OnMouseOver( MousePanelEvent e )
		{
			var tooltip = Hud.Tooltip;
			tooltip.Update( Ability, IsDisabled ) ;
			tooltip.Hover( this );
			tooltip.Show();
		}

		public void Update( ISelectable selectable, BaseAbility ability )
		{
			Selectable = selectable;
			Ability = ability;

			if ( ability.Icon != null )
			{
				Style.BackgroundImage = ability.Icon;
				Style.BackgroundSizeX = Length.Percent( 100f );
				Style.BackgroundSizeY = Length.Percent( 100f );
			}
			else
			{
				Style.BackgroundImage = null;
				Style.BackgroundSizeX = null;
				Style.BackgroundSizeY = null;
			}
		}

		public override void Tick()
		{
			var cooldownTimeLeft = Ability.GetCooldownTimeLeft();

			Countdown.Style.Width = Length.Percent( (100f / Ability.Cooldown) * cooldownTimeLeft );
			Countdown.Style.Dirty();

			Countdown.SetClass( "hidden", cooldownTimeLeft == 0f );

			base.Tick();
		}
	}

	public class ItemCommandQueueable : ItemCommand
	{
		public BaseItem Item { get; private set; }

		protected override void OnClick( MousePanelEvent e )
		{
			if ( IsDisabled ) return;

			var player = (Game.LocalPawn as RTSPlayer);
			var status = Item.CanCreate( player, Selectable );

			if ( status != RequirementError.Success )
			{
				Audio.Play( status );
				return;
			}

			if ( Selectable is UnitEntity builder && Item is BaseBuilding building )
				Items.CreateGhost( builder, building );
			else
				Items.Queue( Selectable.NetworkIdent, Item.NetworkId );

			Audio.Play( "rts.pophappy" );
		}

		protected override void OnMouseOver( MousePanelEvent e )
		{
			var tooltip = Hud.Tooltip;
			tooltip.Update( Item, false, IsDisabled );
			tooltip.Hover( this );
			tooltip.Show();
		}

		public void Update( ISelectable selectable, BaseItem item )
		{
			Selectable = selectable;
			Item = item;

			if ( item.Icon != null )
			{
				Style.BackgroundImage = item.Icon;
				Style.BackgroundSizeX = Length.Percent( 100f );
				Style.BackgroundSizeY = Length.Percent( 100f );
			}
			else
			{
				Style.BackgroundImage = null;
				Style.BackgroundSizeX = null;
				Style.BackgroundSizeY = null;
			}
		}
	}

	public abstract class ItemCommand : Button
	{
		public ISelectable Selectable { get; protected set; }
		public bool IsDisabled { get; private set; }

		public ItemCommand() : base()
		{

		}

		public void Disable()
		{
			AddClass( "disabled" );
			IsDisabled = true;
		}

		protected override void OnMouseOut( MousePanelEvent e )
		{
			Hud.Tooltip.Hide();
		}
	}

	public class ItemOccupantHealth : Panel
	{
		public Panel Foreground { get; private set; }

		public ItemOccupantHealth()
		{
			Foreground = Add.Panel( "foreground" );
		}
	}

	public class ItemOccupantButton : Button
	{
		public ItemOccupantHealth Health { get; private set; }
		public IOccupiableEntity Occupiable { get; private set; }
		public UnitEntity Unit { get; private set; }

		public ItemOccupantButton() : base()
		{
			Health = AddChild<ItemOccupantHealth>( "health" );
		}

		protected override void OnClick( MousePanelEvent e )
		{
			if ( !Unit.IsValid() )
			{
				Audio.Play( "rts.beepvibrato" );
				return;
			}

			Items.Evict( Occupiable.NetworkIdent, Unit.NetworkIdent );

			Audio.Play( "rts.pophappy" );
		}

		protected override void OnMouseOver( MousePanelEvent e )
		{
			if ( !Unit.IsValid() ) return;

			var tooltip = Hud.Tooltip;
			tooltip.Update( Unit.Item, true );
			tooltip.Hover( this );
			tooltip.Show();
		}

		protected override void OnMouseOut( MousePanelEvent e )
		{
			if ( !Unit.IsValid() ) return;
			
			Hud.Tooltip.Hide();
		}

		public void Update( IOccupiableEntity occupiable = null, UnitEntity unit = null )
		{
			Style.BackgroundImage = null;
			Style.BackgroundSizeX = null;
			Style.BackgroundSizeY = null;

			Occupiable = occupiable;
			Unit = unit;

			if ( unit.IsValid() )
			{
				var item = unit.Item;

				if ( item.Icon != null )
				{
					Style.BackgroundImage = item.Icon;
					Style.BackgroundSizeX = Length.Percent( 100f );
					Style.BackgroundSizeY = Length.Percent( 100f );
				}

				Health.SetClass( "hidden", false );
			}
			else
			{
				Health.SetClass( "hidden", true );
			}

			SetClass( "empty", !unit.IsValid() );

			Style.Dirty();
		}

		public override void Tick()
		{
			SetClass( "hidden", Occupiable == null );

			if ( Unit.IsValid() )
			{
				Health.Foreground.Style.Width = Length.Fraction( Unit.Health / Unit.MaxHealth );
				Health.Foreground.Style.Dirty();
			}
		}
	}

	public class ItemOccupantList : Panel
	{
		public IOccupiableEntity Entity { get; private set; }
		public List<ItemOccupantButton> Buttons { get; private set; }

		public ItemOccupantList()
		{
			Buttons = new();

			for ( var i = 0; i < 10; i++ )
			{
				Buttons.Add( AddChild<ItemOccupantButton>() );
			}
		}

		public void Update( IOccupiableEntity occupiable )
		{
			Entity = occupiable;
		}

		public override void Tick()
		{
			base.Tick();

			SetClass( "hidden", Entity == null );

			if ( Entity == null ) return;

			var item = Entity.OccupiableItem;
			var occupants = Entity.GetOccupantsList();

			if ( item.Occupiable.MaxOccupants > 0 && occupants != null )
			{
				var occupantsCount = occupants.Count;

				for ( var i = 0; i < 10; i++ )
				{
					if ( item.Occupiable.MaxOccupants > i )
					{
						if ( occupantsCount > i )
							Buttons[i].Update( Entity, occupants[i] );
						else
							Buttons[i].Update( Entity );
					}
					else
					{
						Buttons[i].Update( null );
					}
				}
			}
			else
			{
				SetClass( "hidden", true );
			}
		}
	}

	public class ItemQueueButton : Button
	{
		public BuildingEntity Building { get; private set; }
		public QueueItem QueueItem { get; private set; }
		public Panel Countdown { get; private set; }

		public ItemQueueButton() : base()
		{
			Countdown = Add.Panel( "countdown" );
		}

		protected override void OnClick( MousePanelEvent e )
		{
			Items.Unqueue( Building.NetworkIdent, QueueItem.Id );
			Audio.Play( "rts.pophappy" );
		}

		protected override void OnMouseOver( MousePanelEvent e )
		{
			var tooltip = Hud.Tooltip;
			tooltip.Update( QueueItem.Item );
			tooltip.Hover( this );
			tooltip.Show();
		}

		protected override void OnMouseOut( MousePanelEvent e )
		{
			Hud.Tooltip.Hide();
		}

		public void Update( QueueItem queueItem, BuildingEntity building = null )
		{
			if ( QueueItem != null )
				RemoveClass( QueueItem.Item.UniqueId.Replace( '.', '_' ) );

			QueueItem = queueItem;
			Building = building;

			Style.BackgroundImage = null;
			Style.BackgroundSizeX = null;
			Style.BackgroundSizeY = null;

			if ( QueueItem != null )
			{
				var item = QueueItem.Item;

				AddClass( item.UniqueId.Replace( '.', '_' ) );

				if ( item.Icon != null )
				{
					Style.BackgroundImage = item.Icon;
					Style.BackgroundSizeX = Length.Percent( 100f );
					Style.BackgroundSizeY = Length.Percent( 100f );
				}	 
			}
		}

		public override void Tick()
		{
			SetClass( "hidden", QueueItem == null );

			if ( QueueItem != null )
			{
				if ( QueueItem.FinishTime > 0f )
					Countdown.Style.Width = Length.Percent( 100f - ((100f / QueueItem.Item.BuildTime) * QueueItem.GetTimeLeft()) );
				else
					Countdown.Style.Width = Length.Percent( 100f );

				Countdown.SetClass( "inactive", QueueItem.FinishTime == 0f );
			}

			base.Tick();
		}
	}

	public class ItemQueueList : Panel
	{
		public BuildingEntity Building { get; private set; }
		public List<ItemQueueButton> Buttons { get; private set; }

		public ItemQueueList()
		{
			Buttons = new();

			for ( var i = 0; i < 10; i++ )
			{
				Buttons.Add( AddChild<ItemQueueButton>() );
			}
		}

		public void Update( BuildingEntity building )
		{
			Building = building;
		}

		public override void Tick()
		{
			SetClass( "hidden", Building == null );

			if ( Building.IsValid() )
			{
				for ( var i = 0; i < 10; i++ )
				{
					if ( Building.Queue.Count > i )
						Buttons[i].Update( Building.Queue[i], Building );
					else
						Buttons[i].Update(  null );
				}
			}

			base.Tick();
		}
	}

	public class ItemCommandList : Panel
	{
		public ISelectable Selectable { get; private set; }
		public List<ItemCommand> Buttons { get; private set; }

		public ItemCommandList()
		{
			Buttons = new();
		}

		public void Update( ISelectable selectable )
		{
			Selectable = selectable;

			Buttons.ForEach( b => b.Delete( true ) );
			Buttons.Clear();

			// Don't show commands for enemy selectables.
			if ( selectable != null && selectable.IsLocalPlayers )
			{
				if ( selectable is UnitEntity unit )
					UpdateCommands( unit.Item.Queueables, unit.AbilityTable );
				else if ( selectable is BuildingEntity building )
					UpdateCommands( building.Item.Queueables, building.AbilityTable );
			}

			Parent.SetClass( "hidden", Buttons.Count == 0 );
		}

		private void AddQueueables<T>( RTSPlayer player, HashSet<string> queueables, List<BaseItem> available, List<BaseItem> unavailable ) where T : BaseItem
		{
			available.Clear();
			unavailable.Clear();

			foreach ( var v in queueables )
			{
				var queueable = Items.Find<BaseItem>( v );
				if ( queueable is not T ) continue;

				if ( queueable.IsAvailable( player, Selectable ) )
				{
					if ( queueable.HasDependencies( player ) )
						available.Add( queueable );
					else
						unavailable.Add( queueable );
				}
			}

			for ( int i = 0; i < available.Count; i++ )
			{
				var v = available[i];
				var button = AddChild<ItemCommandQueueable>( "command" );
				button.Update( Selectable, v );
				Buttons.Add( button );
			}

			for ( int i = 0; i < unavailable.Count; i++ )
			{
				var v = unavailable[i];
				var button = AddChild<ItemCommandQueueable>( "command" );
				button.Disable();
				button.Update( Selectable, v );
				Buttons.Add( button );
			}
		}

		private void UpdateCommands( HashSet<string> queueables, Dictionary<string, BaseAbility> abilities = null )
		{
			var player = Game.LocalPawn as RTSPlayer;

			var availableQueueables = new List<BaseItem>();
			var unavailableQueueables = new List<BaseItem>();

			AddQueueables<BaseUnit>( player, queueables, availableQueueables, unavailableQueueables );
			AddQueueables<BaseBuilding>( player, queueables, availableQueueables, unavailableQueueables );
			AddQueueables<BaseTech>( player, queueables, availableQueueables, unavailableQueueables );
			AddQueueables<BaseUpgrade>( player, queueables, availableQueueables, unavailableQueueables );

			if ( abilities == null ) return;

			var availableAbilities = new List<BaseAbility>();
			var unavailableAbilities = new List<BaseAbility>();

			foreach ( var kv in abilities )
			{
				var ability = kv.Value;

				if ( ability != null && ability.IsAvailable() )
				{
					if ( ability.HasDependencies() )
						availableAbilities.Add( ability );
					else
						unavailableAbilities.Add( ability );
				}
			}

			for ( int i = 0; i < availableAbilities.Count; i++ )
			{
				BaseAbility v = availableAbilities[i];
				var button = AddChild<ItemCommandAbility>( "command" );
				button.Update( Selectable, v );
				Buttons.Add( button );
			}

			for ( int i = 0; i < unavailableAbilities.Count; i++ )
			{
				BaseAbility v = unavailableAbilities[i];
				var button = AddChild<ItemCommandAbility>( "command" );
				button.Disable();
				button.Update( Selectable, v );
				Buttons.Add( button );
			}
		}
	}

	public class ItemInformation : Panel
	{
		public Label Name { get; private set; }
		public Label Desc { get; private set; }
		public IconWithLabel Health { get; private set; }
		public IconWithLabel Kills { get; private set; }
		public IconWithLabel Damage { get; private set; }
		public ISelectable Selectable { get; private set; }
		public ItemQueueList QueueList { get; private set; }
		public ItemLabelValues ItemLabels { get; private set; }
		public ResistanceValues ResistanceValues { get; private set; }
		public ItemOccupantList OccupantList { get; private set; }


		public ItemInformation()
		{
			Name = Add.Label( "", "name" );
			Desc = Add.Label( "", "desc" );
			ItemLabels = AddChild<ItemLabelValues>( "itemlabels" );
			Health = AddChild<IconWithLabel>( "health" );
			Damage = AddChild<IconWithLabel>( "damage" );
			Kills = AddChild<IconWithLabel>( "kills" );
			ResistanceValues = AddChild<ResistanceValues>( "resistances" );
			QueueList = AddChild<ItemQueueList>();
			OccupantList = AddChild<ItemOccupantList>();

			foreach ( var kv in Resistances.Table )
			{
				ResistanceValues.AddResistance( kv.Value );
			}
		}

		public void Update( ISelectable selectable )
		{
			Selectable = selectable;

			if ( selectable is UnitEntity unit )
				UpdateUnit( unit );
			else if ( selectable is BuildingEntity building )
				UpdateBuilding( building );
		}

		public override void Tick()
		{
			if ( Selectable == null ) return;

			Kills.SetVisible( false );
			Damage.SetVisible( false );

			if ( Selectable is UnitEntity unit )
			{
				if ( unit.Weapon.IsValid() )
				{
					Kills.Label.Text = $"{unit.Kills}";
					Kills.SetVisible( true );

					var baseDamage = unit.Weapon.BaseDamage;
					var fullDamage = unit.Weapon.GetDamage();

					if ( fullDamage > 0 )
					{
						var difference = fullDamage - baseDamage;
						var perSecond = unit.Weapon.GetDamagePerSecond();

						if ( difference > 0 )
							Damage.Label.Text = $"{baseDamage}+{difference} ({perSecond} DPS)";
						else
							Damage.Label.Text = $"{baseDamage} ({perSecond} DPS)";

						Damage.SetClass( "hidden", false );
					}
				}
			}

			Health.Label.Text = $"{Selectable.Health.CeilToInt()} / {Selectable.MaxHealth.CeilToInt()}";

			base.Tick();
		}

		private void UpdateBuilding( BuildingEntity entity )
		{
			var data = entity.Item;

			Name.Text = data.Name;
			Desc.Text = data.Description;

			Name.Style.FontColor = data.Color;
			Name.Style.Dirty();

			QueueList.Update( entity );
			OccupantList.Update( entity );

			if ( data.Labels.Count > 0 )
			{
				ItemLabels.SetVisible( true );
				ItemLabels.Clear();

				foreach ( var label in data.Labels )
				{
					ItemLabels.AddItemLabel( label );
				}
			}
			else
			{
				ItemLabels.SetVisible( false );
			}

			var resistances = data.Resistances;

			ResistanceValues.SetVisible( resistances.Count > 0 );

			foreach ( var kv in ResistanceValues.Values )
			{
				if ( resistances.TryGetValue( kv.Key, out var resistance ) )
				{
					kv.Value.Update( resistance );
					kv.Value.SetVisible( true );
				}
				else
				{
					kv.Value.SetVisible( false );
				}
			}
		}

		private void UpdateUnit( UnitEntity entity ) 
		{
			var data = entity.Item;

			Name.Text = data.Name;
			Desc.Text = data.Description;

			Name.Style.FontColor = data.Color;
			Name.Style.Dirty();

			QueueList.Update( null );
			OccupantList.Update( entity );

			if ( data.Labels.Count > 0 )
			{
				ItemLabels.SetVisible( true );
				ItemLabels.Clear();

				foreach ( var label in data.Labels )
				{
					ItemLabels.AddItemLabel( label );
				}
			}
			else
			{
				ItemLabels.SetVisible( false );
			}

			var resistances = entity.ResistancesTable;

			ResistanceValues.SetVisible( resistances.Count > 0 );

			foreach ( var kv in ResistanceValues.Values )
			{
				if ( resistances.TryGetValue( kv.Key, out var resistance ) )
				{
					kv.Value.Update( resistance );
					kv.Value.SetVisible( true );
				}
				else
				{
					kv.Value.SetVisible( false );
				}
			}
		}
	}

	public class ItemMultiHealth : Panel
	{
		public Panel Foreground { get; private set; }

		public ItemMultiHealth()
		{
			Foreground = Add.Panel( "foreground" );
		}
	}

	public class ItemMultiIcon : Button
	{
		public ItemMultiHealth Health { get; private set; }
		public ISelectable Selectable { get; private set; }
		public BaseItem Item { get; private set; }

		public ItemMultiIcon()
		{
			Health = AddChild<ItemMultiHealth>( "health" );
		}

		protected override void OnClick( MousePanelEvent e )
		{
			if ( Input.Down( "duck" ) )
				Items.RefineSelection( Selectable.ItemNetworkId.ToString() );
			else
				Items.Select( Selectable.NetworkIdent.ToString() );
			
			Audio.Play( "rts.pophappy" );
		}

		protected override void OnMouseOver( MousePanelEvent e )
		{
			var tooltip = Hud.Tooltip;
			tooltip.Update( Item, true );
			tooltip.Hover( this );
			tooltip.Show();
		}

		protected override void OnMouseOut( MousePanelEvent e )
		{
			Hud.Tooltip.Hide();
		}

		public override void Tick()
		{
			if ( !(Selectable as Entity).IsValid() )
			{
				Delete();
				return;
			}

			Health.Foreground.Style.Width = Length.Fraction( Selectable.Health / Selectable.MaxHealth );
			Health.Foreground.Style.Dirty();
		}

		public void Update( ISelectable selectable )
		{
			Selectable = selectable;

			if ( selectable is UnitEntity unit )
				Item = unit.Item;
			else if ( selectable is BuildingEntity building )
				Item = building.Item;

			if ( Item.Icon != null )
			{
				Style.BackgroundImage = Item.Icon;
				Style.BackgroundSizeX = Length.Percent( 100f );
				Style.BackgroundSizeY = Length.Percent( 100f );
			}
			else
			{
				Style.BackgroundImage = null;
				Style.BackgroundSizeX = null;
				Style.BackgroundSizeY = null;
			}
		}
	}

	public class ItemMultiDisplay : Panel
	{
		public Panel Container { get; private set; }
		public ItemMultiIcon Active { get; private set; }
		public List<ItemMultiIcon> Icons { get; private set; }
		public List<ISelectable> Items { get; private set; }

		public ItemMultiDisplay()
		{
			Icons = new();
			Container = Add.Panel( "container" );
		}

		public void SetActive( ISelectable item )
		{
			if ( Active != null ) Active.RemoveClass( "active" );

			var icon = Icons.Find( v => v.Selectable == item );

			if ( icon != null )
			{
				Active = icon;
				Active.AddClass( "active" ); 
			}
		}

		public void Update( List<ISelectable> items )
		{
			Active = null;
			Items = items;

			foreach ( var icon in Icons )
			{
				icon.Delete( true );
			}

			Icons.Clear();

			Items.Sort( ( a, b ) => a.ItemNetworkId.CompareTo( b.ItemNetworkId ) );

			foreach ( var item in items )
			{
				var icon = Container.AddChild<ItemMultiIcon>( "icon" );
				icon.Update( item );
				Icons.Add( icon );
			}
		}
	}

	[StyleSheet( "/ui/SelectedItem.scss" )]
	public class SelectedItem : Panel
	{
		public static SelectedItem Instance { get; private set; }

		public ISelectable Active { get; private set; }
		public List<ISelectable> Items { get; private set; }
		public ItemMultiDisplay MultiDisplay { get; private set; }
		public ItemInformation Information { get; private set; }
		public ItemCommandList CommandList { get; private set; }
		public Panel Left { get; private set; }
		public Panel Right { get; private set; }

		public SelectedItem()
		{
			Left = Add.Panel( "left" );
			Right = Add.Panel( "right" );

			MultiDisplay = Left.AddChild<ItemMultiDisplay>();
			Information = Left.AddChild<ItemInformation>();
			CommandList = Right.AddChild<ItemCommandList>();

			Items = new();

			Instance = this;
		}

		public void Update( List<ISelectable> items )
		{
			Items = items;

			if ( items.Count == 0 ) return;

			if ( items.Count > 1 )
			{
				if ( Active != null && !items.Contains( Active ) )
				{
					Active = items[0];
				}

				MultiDisplay.Update( items );
				MultiDisplay.SetActive( Active );
			}
			else
			{
				Information.Update( items[0] );
				Active = items[0];
			}

			CommandList.Update( Active );
		}

		public void Update( IList<Entity> entities )
		{
			Update( entities.Cast<ISelectable>().ToList() );
		}

		public void Next()
		{
			if ( Items.Count <= 1 ) return;

			var index = Items.IndexOf( Active );

			if ( index >= 0 )
			{
				var nextIndex = index + 1;

				if ( nextIndex >= Items.Count )
					nextIndex = 0;

				Active = Items[nextIndex];

				MultiDisplay.SetActive( Active );
				CommandList.Update( Active );
			}
		}

		public override void Tick()
		{
			if ( !RTSGame.Entity.IsValid() ) return;

			var isHidden = true;

			if ( Hud.IsLocalPlaying() )
			{
				if ( Items.Count > 0 )
					isHidden = false;
			}

			if ( Items.Count > 0 )
			{
				MultiDisplay.SetClass( "hidden", Items.Count < 2 );
				Information.SetClass( "hidden", Items.Count > 1 );
			}

			SetClass( "hidden", isHidden );

			base.Tick();
		}
	}
}
