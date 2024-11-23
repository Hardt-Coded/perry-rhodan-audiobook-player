namespace PerryRhodan.AudiobookPlayer.Controls


open System
open System.Collections
open System.Collections.Generic
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.Generators
open Avalonia.Controls.Primitives
open Avalonia.Controls.Templates
open Avalonia.Data
open Avalonia.Input
open Avalonia.Layout
open Avalonia.LogicalTree
open Avalonia.Metadata
open Avalonia.VisualTree
open System.ComponentModel

type VirtualPanelScrollMode =
    | Smooth = 0
    | Item = 1


type VirtualPanelLayout =
    | Stack = 0
    | Wrap = 1

type VirtualPanel() as self =
    inherit Control()
    
    
    // #region Properties
    static let LayoutProperty =
        AvaloniaProperty.Register<VirtualPanel, VirtualPanelLayout>("Layout")

    static let ScrollModeProperty =
        AvaloniaProperty.Register<VirtualPanel, VirtualPanelScrollMode>("ScrollMode")

    static let ItemHeightProperty =
        AvaloniaProperty.Register<VirtualPanel, double>("ItemHeight", Double.NaN)

    static let ItemWidthProperty =
        AvaloniaProperty.Register<VirtualPanel, double>("ItemWidth", Double.NaN)

    static let ItemsSourceProperty =
        AvaloniaProperty.Register<VirtualPanel, IEnumerable>("ItemsSource")

    static let SelectedItemProperty =
        AvaloniaProperty.RegisterDirect<VirtualPanel, obj>(
            "SelectedItem",
            (fun o -> o.SelectedItem),
            (fun o v -> o.SelectedItem <- v),
            defaultBindingMode = BindingMode.TwoWay)

    static let ItemTemplateProperty =
        AvaloniaProperty.Register<VirtualPanel, IDataTemplate>("ItemTemplate")

    let mutable _selectedItem : obj = null
    
    let mutable _extent = Size()
    let mutable _offset = Vector()
    let mutable _viewport = Size()
    let mutable _canHorizontallyScroll = false
    let mutable _canVerticallyScroll = false
    let _isLogicalScrollEnabled = true
    let mutable _scrollSize = Size(1.0, 1.0)
    let mutable _pageScrollSize = Size(10.0, 10.0)
    let scrollInvalidatedEvent = Event<EventHandler, EventArgs>()
    
    let mutable _childIndexChangedEvent = Event<EventHandler<ChildIndexChangedEventArgs>, ChildIndexChangedEventArgs>()
    
    let mutable _startIndex = -1
    let mutable _endIndex = -1
    let mutable _visibleCount = -1
    let mutable _scrollOffset = 0.0
    let _recycled = System.Collections.Generic.Stack<Control>()
    let _controls = SortedDictionary<int, Control>()
    let _children = List<Control>()
    
    // Interface implementations will be added later

    // #region Util
    member private this.GetItemsCount(items: IEnumerable) =
        if isNull items then
            0
        else
            match items with
            | :? IList as list -> list.Count
            | _ -> 0 // TODO: Support other IEnumerable types.
    // #endregion

    // #region ILogicalScrollable
    

    member private this.CoerceOffset(value: Vector) =
        let scrollable = this :> ILogicalScrollable
        let maxX = Math.Max(scrollable.Extent.Width - scrollable.Viewport.Width, 0.0)
        let maxY = Math.Max(scrollable.Extent.Height - scrollable.Viewport.Height, 0.0)
        let clamp (val': double) (min: double) (max: double) =
            if val' < min then min elif val' > max then max else val'
        Vector(clamp value.X 0.0 maxX, clamp value.Y 0.0 maxY)

    interface IScrollable with
        member this.Extent = _extent
        member this.Offset
            with get() = _offset
            and set(value) =
                _offset <- this.CoerceOffset(value)
                this.InvalidateMeasure()
        member this.Viewport = _viewport

    interface ILogicalScrollable with
        member this.BringIntoView(target: Control, targetRect: Rect) = false
        member this.GetControlInDirection(direction: NavigationDirection, from: Control) = null
        member this.RaiseScrollInvalidated(e: EventArgs) = scrollInvalidatedEvent.Trigger(this, e)
        member this.CanHorizontallyScroll
            with get() = _canHorizontallyScroll
            and set(value) = _canHorizontallyScroll <- value
        member this.CanVerticallyScroll
            with get() = _canVerticallyScroll
            and set(value) = _canVerticallyScroll <- value
        member this.IsLogicalScrollEnabled = _isLogicalScrollEnabled
        member this.ScrollSize = _scrollSize
        member this.PageScrollSize = _pageScrollSize
        [<CLIEvent>]
        member this.ScrollInvalidated = scrollInvalidatedEvent.Publish

    member private this.InvalidateScrollable() =
        let scrollable = this :> ILogicalScrollable
        scrollable.RaiseScrollInvalidated(EventArgs.Empty)
    // #endregion

    // #region IChildIndexProvider
    

    interface IChildIndexProvider with
        member this.GetChildIndex(child: ILogical) =
            match child with
            | :? Control as control ->
                _controls
                |> Seq.tryFind (fun kvp -> obj.ReferenceEquals(kvp.Value, control))
                |> function
                    | Some kvp -> kvp.Key
                    | None -> -1
            | _ -> -1

        member this.TryGetTotalCount(count: byref<int>) =
            count <- this.GetItemsCount(this.ItemsSource)
            true

        [<CLIEvent>]
        member this.ChildIndexChanged = _childIndexChangedEvent.Publish

    member private this.RaiseChildIndexChanged() =
        _childIndexChangedEvent.Trigger(this, ChildIndexChangedEventArgs(null, -1))
    // #endregion

    

    member this.Layout
        with get() = this.GetValue(LayoutProperty)
        and set(value) = this.SetValue(LayoutProperty, value) |> ignore

    member this.ScrollMode
        with get() = this.GetValue(ScrollModeProperty)
        and set(value) = this.SetValue(ScrollModeProperty, value) |> ignore

    member this.ItemHeight
        with get() = this.GetValue(ItemHeightProperty)
        and set(value) = this.SetValue(ItemHeightProperty, value) |> ignore

    member this.ItemWidth
        with get() = this.GetValue(ItemWidthProperty)
        and set(value) = this.SetValue(ItemWidthProperty, value) |> ignore

    member this.ItemsSource
        with get() = this.GetValue(ItemsSourceProperty)
        and set(value) = this.SetValue(ItemsSourceProperty, value) |> ignore

    member this.SelectedItem
        with get() = _selectedItem
        and set(value) = this.SetAndRaise(SelectedItemProperty, &_selectedItem, value) |> ignore

    [<Content>]
    member this.ItemTemplate
        with get() = this.GetValue(ItemTemplateProperty)
        and set(value) = this.SetValue(ItemTemplateProperty, value) |> ignore
    // #endregion

    // #region Events
    abstract member OnContainerMaterialized: Control * int -> unit
    default this.OnContainerMaterialized(container, index) = ()

    abstract member OnContainerDematerialized: Control * int -> unit
    default this.OnContainerDematerialized(container, index) = ()

    abstract member OnContainerRecycled: Control * int -> unit
    default this.OnContainerRecycled(container, index) = ()
    // #endregion

    // #region Layout
    

    member this.Children = _children :> IReadOnlyList<Control>

    member private this.UpdateScrollable(width: double, height: double, totalWidth: double) : Size =
        let itemCount = this.GetItemsCount(this.ItemsSource)
        let layout = this.Layout
        let itemHeight = this.ItemHeight
        let itemWidth = this.ItemWidth

        let totalHeight =
            match layout with
            | VirtualPanelLayout.Stack ->
                float itemCount * itemHeight
            | VirtualPanelLayout.Wrap ->
                let mutable itemsPerRow = int (width / itemWidth)
                if itemsPerRow <= 0 then itemsPerRow <- 1
                Math.Ceiling(float itemCount / float itemsPerRow) * itemHeight
            | _ -> raise (ArgumentOutOfRangeException())

        let extent = Size(totalWidth, totalHeight)

        _viewport <- Size(width, height)
        _extent <- extent
        _scrollSize <- Size(16.0, 16.0)
        _pageScrollSize <- Size(_viewport.Width, _viewport.Height)

        extent

    member private this.AddChild(control: Control) =
        this.LogicalChildren.Add(control)
        this.VisualChildren.Add(control)
        _children.Add(control)

    member private this.RemoveChildren(controls: HashSet<Control>) =
        let controlsSeq = controls :> seq<_>
        for control in controlsSeq do
            this.LogicalChildren.Remove(control) |> ignore
            this.VisualChildren.Remove(control) |> ignore
            _children.Remove(control) |> ignore

    member private this.ClearChildren() =
        this.LogicalChildren.Clear()
        this.VisualChildren.Clear()
        _children.Clear()

    member private this.InvalidateChildren(width: double, height: double, offset: double) =
        match this.ItemsSource with
        | :? IList as items ->
            let itemCount = this.GetItemsCount(items)
            let layout = this.Layout
            let itemHeight = this.ItemHeight
            let itemWidth = this.ItemWidth

            _scrollOffset <- if this.ScrollMode = VirtualPanelScrollMode.Smooth then offset % itemHeight else 0.0
            let size = height + _scrollOffset

            let mutable itemsPerRow = int (width / itemWidth)
            if itemsPerRow <= 0 then itemsPerRow <- 1

            match layout with
            | VirtualPanelLayout.Stack ->
                _startIndex <- int (offset / itemHeight)
                _visibleCount <- int (size / itemHeight)
            | VirtualPanelLayout.Wrap ->
                _startIndex <- int (offset / itemHeight) * itemsPerRow
                _visibleCount <- int (size / itemHeight) * itemsPerRow
            | _ -> raise (ArgumentOutOfRangeException())

            if size % itemHeight > 0.0 && height > 0.0 then
                _visibleCount <- _visibleCount + 1

            match layout with
            | VirtualPanelLayout.Stack ->
                _endIndex <- (_startIndex + _visibleCount) - 1
            | VirtualPanelLayout.Wrap ->
                _endIndex <- (_startIndex + _visibleCount + itemsPerRow) - 1
            | _ -> raise (ArgumentOutOfRangeException())

            if itemCount = 0 || isNull this.ItemTemplate then
                this.ClearChildren()
                this.RaiseChildIndexChanged()
            else
                this.InvalidateContainers(items, itemCount)
                this.RaiseChildIndexChanged()
        | _ ->
            _scrollOffset <- 0.0

    member private this.InvalidateContainers(items: IList, itemCount: int) =
        if _startIndex >= itemCount || isNull this.ItemTemplate then
            ()
        else
            let toRemove = List<int>()
            for kvp in _controls do
                if kvp.Key < _startIndex || kvp.Key > _endIndex then
                    toRemove.Add(kvp.Key)
            let childrenRemove = HashSet<Control>()
            for remove in toRemove do
                let control = _controls.[remove]
                control.DataContext <- null
                _recycled.Push(control)
                _controls.Remove(remove) |> ignore
                childrenRemove.Add(control) |> ignore
                this.OnContainerDematerialized(control, remove)

            for i = _startIndex to _endIndex do
                if i < 0 || i >= itemCount then
                    ()
                elif not (_controls.ContainsKey(i)) then
                    let param = items.[i]
                    let control =
                        if _recycled.Count > 0 then
                            let control = _recycled.Pop()
                            control.DataContext <- param
                            _controls.[i] <- control
                            if not (childrenRemove.Contains(control)) then
                                this.AddChild(control)
                            else
                                childrenRemove.Remove(control) |> ignore
                            this.OnContainerRecycled(control, i)
                            control
                        else
                            let content = if isNull param then null else this.ItemTemplate.Build(param)
                            let control = ContentControl(Content = content)
                            control.DataContext <- param
                            _controls.[i] <- control
                            this.AddChild(control)
                            this.OnContainerMaterialized(control, i)
                            control
                    ()
            this.RemoveChildren(childrenRemove)

    override this.MeasureOverride(availableSize: Size) =
        let availableSize = this.UpdateScrollable(availableSize.Width, availableSize.Height, availableSize.Width)
        this.InvalidateChildren(_viewport.Width, _viewport.Height, _offset.Y)
        if _controls.Count > 0 then
            let layout = this.Layout
            let itemHeight = this.ItemHeight
            let itemWidth = this.ItemWidth
            for kvp in _controls do
                match layout with
                | VirtualPanelLayout.Stack ->
                    let size = Size(_viewport.Width, itemHeight)
                    kvp.Value.Measure(size)
                | VirtualPanelLayout.Wrap ->
                    let size = Size(itemWidth, itemHeight)
                    kvp.Value.Measure(size)
                | _ -> raise (ArgumentOutOfRangeException())
        availableSize

    override this.ArrangeOverride(finalSize: Size) =
        let finalSize = this.UpdateScrollable(finalSize.Width, finalSize.Height, finalSize.Width)
        let layout = this.Layout
        let itemHeight = this.ItemHeight
        let itemWidth = this.ItemWidth

        this.InvalidateChildren(_viewport.Width, _viewport.Height, _offset.Y)
        this.InvalidateScrollable()

        let scrollOffsetX = 0.0 // TODO: _offset.X
        let scrollOffsetY = _scrollOffset

        if _controls.Count > 0 then
            let mutable x = if scrollOffsetX = 0.0 then 0.0 else -scrollOffsetX
            let mutable y = if scrollOffsetY = 0.0 then 0.0 else -scrollOffsetY
            match layout with
            | VirtualPanelLayout.Stack ->
                for kvp in _controls do
                    let rect = Rect(Point(x, y), Size(_viewport.Width, itemHeight))
                    kvp.Value.Arrange(rect)
                    y <- y + itemHeight
            | VirtualPanelLayout.Wrap ->
                let mutable column = 0
                let itemsPerRow = int (_viewport.Width / itemWidth)
                for kvp in _controls do
                    let rect = Rect(Point(x + itemWidth * float column, y), Size(itemWidth, itemHeight))
                    kvp.Value.Arrange(rect)
                    column <- column + 1
                    if column >= itemsPerRow then
                        y <- y + itemHeight
                        column <- 0
            | _ -> raise (ArgumentOutOfRangeException())
        finalSize
    // #endregion

    override this.OnPropertyChanged(change: AvaloniaPropertyChangedEventArgs) =
        base.OnPropertyChanged(change)
        if change.Property = LayoutProperty ||
           change.Property = ScrollModeProperty ||
           change.Property = ItemWidthProperty ||
           change.Property = ItemHeightProperty then
            this.InvalidateMeasure()
            
        if change.Property = ItemsSourceProperty then
            this.ClearChildren()
            _recycled.Clear()
            _controls.Clear()
            this.InvalidateMeasure()
            this.InvalidateScrollable()
            this.InvalidateVisual()
            this.InvalidateArrange()
            
            
            
            
            
            
            


