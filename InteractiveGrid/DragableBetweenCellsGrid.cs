#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

#endregion

namespace InteractiveGrid
{
    /// <summary>
    /// </summary>
    /// <remarks>
    ///     Grigoriev Igor 08.06.2016
    /// </remarks>
    public class DragableBetweenCellsGrid : Grid
    {
        #region Fields

        private ControlState _initalState;

        private bool _isInDrag;
        private Point _mouseOffset;
        private Win32Point _prevPosition = default(Win32Point);
        private double _renderOriginX;
        private double _renderOriginY;
        private bool _transformX;
        private bool _transformY;

        #endregion

        #region Dependency properties registrators

        public static readonly DependencyProperty EditModeAttachedProperty =
            DependencyProperty.RegisterAttached("EditMode", typeof(bool), typeof(DragableBetweenCellsGrid),
                new PropertyMetadata(default(bool)));

        public static readonly DependencyProperty EditModeEnabledProperty = DependencyProperty.Register(
            "EditModeEnabled", typeof(bool), typeof(DragableBetweenCellsGrid),
            new PropertyMetadata(default(bool), EnableDisableAll));

        public static readonly DependencyProperty CurrentElementProperty = DependencyProperty.Register(
            "CurrentElement", typeof(FrameworkElement), typeof(DragableBetweenCellsGrid),
            new PropertyMetadata(default(FrameworkElement)));

        public static readonly DependencyProperty CollisionResultProperty = DependencyProperty.Register(
            "CollisionResult", typeof(CollisionResult), typeof(DragableBetweenCellsGrid),
            new PropertyMetadata(CollisionResult.RollbackIfCollideAny));

        #endregion

        #region PUBLIC METHODS
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkChildAsEditable(UIElement child)
        {
            child.SetValue(EditModeAttachedProperty, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkChildAsNonEditable(UIElement child)
        {
            child.SetValue(EditModeAttachedProperty, false);
        }
        #endregion

        #region PUBLIC PROPERTIES

        /// <summary>
        ///     Gets or sets edit mode. All children marks as movable
        ///     (equivalient of all childs calls <see cref="MarkChildAsEditable" /> \ <see cref="MarkChildAsNonEditable" />)
        /// </summary>
        public bool EditModeEnabled
        {
            get { return (bool)GetValue(EditModeEnabledProperty); }
            set { SetValue(EditModeEnabledProperty, value); }
        }

        public FrameworkElement CurrentElement
        {
            get { return (FrameworkElement)GetValue(CurrentElementProperty); }
            set { SetValue(CurrentElementProperty, value); }
        }

        public CollisionResult CollisionResult
        {
            get { return (CollisionResult)GetValue(CollisionResultProperty); }
            set { SetValue(CollisionResultProperty, value); }
        }

        public event Action<ControlState, ControlState, IEnumerable<UIElement>> CustomCollisionPolicy;

        #endregion

        #region Mouse actions

        private void root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CurrentElement = sender as FrameworkElement;

            if (CurrentElement != null && GetEditMode(CurrentElement))
            {
                var pointInside = Mouse.GetPosition(CurrentElement);

                var offset = Math.Min(CurrentElement.ActualHeight, CurrentElement.ActualWidth)*0.1;

                _transformX = pointInside.X < offset || CurrentElement.ActualWidth - pointInside.X < offset;
                _transformY = pointInside.Y < offset || CurrentElement.ActualHeight - pointInside.Y < offset;

                // if move all, check if center
                if (!_transformX && !_transformY)
                {
                    if (pointInside.X > 3.0*CurrentElement.ActualWidth/4.0 ||
                        pointInside.X < CurrentElement.ActualWidth/4.0 ||
                        pointInside.Y > 3.0*CurrentElement.ActualHeight/4.0 ||
                        pointInside.Y < CurrentElement.ActualHeight/4.0)
                    {
                        return;
                    }
                }

                if (_transformX)
                {
                    _renderOriginX = pointInside.X < offset ? 1.0 : 0;
                    Mouse.OverrideCursor = Mouse.OverrideCursor ?? Cursors.SizeWE;
                }
                else
                {
                    _renderOriginX = 0;
                }

                if (_transformY)
                {
                    _renderOriginY = pointInside.Y < offset ? 1.0 : 0;
                    Mouse.OverrideCursor = Mouse.OverrideCursor ?? Cursors.SizeNS;
                }
                else
                {
                    _renderOriginY = 0;
                }

                if (_transformX && _transformY)
                {
                    if ((Math.Abs(_renderOriginX) < double.Epsilon && Math.Abs(_renderOriginY - 1.0) < double.Epsilon) ||
                        (Math.Abs(_renderOriginY) < double.Epsilon && Math.Abs(_renderOriginX - 1.0) < double.Epsilon))
                        Mouse.OverrideCursor = Cursors.SizeNESW;
                    else
                    {
                        Mouse.OverrideCursor = Cursors.SizeNWSE;
                    }
                }

                int mouseCol, mouseRow;
                SetZIndex(CurrentElement, 1000);
                _mouseOffset = Mouse.GetPosition(this);
                GetPosition(out mouseCol, out mouseRow);

                _initalState.ZIndex = GetZIndex(CurrentElement);
                _initalState.Margin = CurrentElement.Margin;
                _initalState.Element = CurrentElement;
                _initalState.ColSpan = GetColumnSpan(CurrentElement);
                _initalState.RowSpan = GetRowSpan(CurrentElement);
                _initalState.Col = GetColumn(CurrentElement);
                _initalState.Row = GetRow(CurrentElement);
                _initalState.MouseGridRowPosition = mouseRow;
                _initalState.MouseGridColumnPosition = mouseCol;

                _isInDrag = true;

                CurrentElement.CaptureMouse();
                e.Handled = true;
            }
        }

        private void root_MouseMove(object sender, MouseEventArgs e)
        {
            var element = sender as FrameworkElement;

            if (!ValidateElement(element))
            {
                return;
            }

            if (_transformX || _transformY)
            {
                var minHeight = RowDefinitions.Min(rd => rd.ActualHeight);
                var minLength = ColumnDefinitions.Min(rd => rd.ActualWidth);


                var mouseDelta = Mouse.GetPosition(this);
                mouseDelta.Offset(-_mouseOffset.X, -_mouseOffset.Y);

                var scaleY = 1 +
                             (Math.Abs(_renderOriginY - 1.0) < double.Epsilon ? -1 : 1)*mouseDelta.Y/
                             CurrentElement.RenderSize.Height;
                var scaleX = 1 +
                             (Math.Abs(_renderOriginX - 1.0) < double.Epsilon ? -1 : 1)*mouseDelta.X/
                             CurrentElement.RenderSize.Width;

                if (RenderSize.Height*scaleY <= minHeight || RenderSize.Width*scaleX <= minLength)
                {
                    UnsafeMethods.SetCursorPos(_prevPosition.X, _prevPosition.Y);
                    return;
                }

                var scale = ((TransformGroup) CurrentElement.RenderTransform).Children.OfType<ScaleTransform>().First();
                CurrentElement.RenderTransformOrigin = new Point(_renderOriginX, _renderOriginY);
                if (_transformY)
                {
                    scale.ScaleY = scaleY;
                }

                if (_transformX)
                {
                    scale.ScaleX = scaleX; // < 1 ? 1.0 : scaleX;
                }
            }
            else
            {
                var mouseDelta = Mouse.GetPosition(this);
                mouseDelta.Offset(-_mouseOffset.X, -_mouseOffset.Y);

                if (element != null)
                    element.Margin = new Thickness(
                        Margin.Left + mouseDelta.X,
                        Margin.Top + mouseDelta.Y,
                        Margin.Right - mouseDelta.X,
                        Margin.Bottom - mouseDelta.Y);
            }

            UnsafeMethods.GetCursorPos(ref _prevPosition);


            e.Handled = true;
        }

        private void root_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isInDrag)
            {
                CurrentElement = null;
                return;
            }

            var element = CurrentElement;

            if (element == null) return;

            element.ReleaseMouseCapture();

            int col, row;
            GetPosition(out col, out row);

            // here we need to check for collision

            var result = GetNewPosition(element, col, row);
            var newState = new ControlState
            {
                Row = result.Item1,
                RowSpan = result.Item2,
                Col = result.Item3,
                ColSpan = result.Item4
            };

            IEnumerable<UIElement> collisions = new List<UIElement>();

            if (CollisionResult != CollisionResult.None)
            {
                collisions = CheckCollisions(newState);
            }

            // if noone is collides, just set new position to element
            if (!collisions.Any())
            {
                OnCollisionNone(element, newState);
            }
            else
            {
                switch (CollisionResult)
                {
                    case CollisionResult.RollbackIfCollideAny:
                    {
                        OnCollisionRollback(element, _initalState);
                        break;
                    }
                    case CollisionResult.CustomBehavour:
                    {
                        if (CustomCollisionPolicy != null)
                            OnCustomCollisionPolicy(newState,collisions);
                        else
                            OnCollisionNone(element, newState);
                        break;
                    }
                }
            }


            var scale = ((TransformGroup) element.RenderTransform).Children.OfType<ScaleTransform>().First();
            scale.ScaleX = 1.0;
            scale.ScaleY = 1.0;
            SetZIndex(element, _initalState.ZIndex);
            element.Margin = _initalState.Margin;
            Mouse.OverrideCursor = null;
            CurrentElement = null;

            _isInDrag = false;
            e.Handled = true;
        }

        #endregion

        #region Collision reactions

        private IEnumerable<UIElement> CheckCollisions(ControlState state)
        {
            int row = state.Row, rowSpan = state.RowSpan, col = state.Col, colSpan = state.ColSpan;

            var collisions = new List<UIElement>(3);

            var currentFigure = new List<Point>(rowSpan*colSpan);

            for (var i = row; i < row + rowSpan; i++)
            {
                for (var j = col; j < col + colSpan; j++)
                {
                    currentFigure.Add(new Point(i, j));
                }
            }

            foreach (var child in Children.OfType<UIElement>().ToArray())
            {
                if (ReferenceEquals(child, CurrentElement))
                    continue;

                var otherFigure = new List<Point>();
                for (var i = GetRow(child); i < GetRow(child) + GetRowSpan(child); i++)
                {
                    for (var j = GetColumn(child); j < GetColumn(child) + GetColumnSpan(child); j++)
                    {
                        otherFigure.Add(new Point(i, j));
                    }
                }

                if (otherFigure.Intersect(currentFigure).Any())
                {
                    collisions.Add(child);
                }
            }

            return collisions;
        }

        private void OnCustomCollisionPolicy(ControlState newState, IEnumerable<UIElement> objectsWithCollision)
        {
            CustomCollisionPolicy?.Invoke(_initalState,newState, objectsWithCollision);
        }

        private void OnCollisionRollback(UIElement element, ControlState initState)
        {
            SetRow(element, initState.Row);
            SetRowSpan(element, initState.RowSpan);
            SetColumn(element, initState.Col);
            SetColumnSpan(element, initState.ColSpan);
        }

        private void OnCollisionNone(UIElement element, ControlState state)
        {
            SetRow(element, state.Row);
            SetRowSpan(element, state.RowSpan);
            SetColumn(element, state.Col);
            SetColumnSpan(element, state.ColSpan);
        }

        #endregion

        #region Position determination methods

        /// <summary>
        ///     Returns row, rowspan, col, colspan
        /// </summary>
        /// <param name="element"></param>
        /// <param name="col"></param>
        /// <param name="row"></param>
        /// <returns></returns>
        private Tuple<int, int, int, int> GetNewPosition(FrameworkElement element, int col, int row)
        {
            int r = _initalState.Row, rs = _initalState.RowSpan, c = _initalState.Col, cs = _initalState.ColSpan;

            // if it was resizing
            if (_transformX || _transformY)
            {
                // if by x
                if (_transformX)
                {
                    ApplyNewPositionIfScaling(element, col, _initalState.Col, _initalState.ColSpan, _renderOriginX > 0.5,
                        ref c, ref cs);
                }

                // if by y
                if (_transformY)
                {
                    ApplyNewPositionIfScaling(element, row, _initalState.Row, _initalState.RowSpan, _renderOriginY > 0.5,
                        ref r, ref rs);
                }
            }
            else
            {
                rs = _initalState.RowSpan;
                cs = _initalState.ColSpan;

                var rowFinal = row - (_initalState.MouseGridRowPosition - _initalState.Row);
                r = rowFinal < 0 ? 0 : rowFinal;

                var columnFinal = col - (_initalState.MouseGridColumnPosition - _initalState.Col);
                c = columnFinal < 0 ? 0 : columnFinal;

                Debug.Print($"{element} moved");
            }

            return new Tuple<int, int, int, int>(r, rs, c, cs);
        }

        private void ApplyNewPositionIfScaling(FrameworkElement element, int currentMousePosition,
            int prevGridPosition, int prevLength, bool renderOriginLessHalf, ref int pos, ref int span)
        {
            if (pos < 0) throw new ArgumentOutOfRangeException(nameof(pos));
            if (span <= 0) throw new ArgumentOutOfRangeException(nameof(span));

            // user moved left side and shrinked control
            if (currentMousePosition < prevGridPosition)
            {
                var newColSpan = prevLength + prevGridPosition - currentMousePosition;
                pos = currentMousePosition;
                span = newColSpan <= 0 ? 1 : newColSpan;
            }
            else
            {
                // user moved left side and enlarged control
                if (renderOriginLessHalf)
                {
                    var newColSpan = prevLength + prevGridPosition - currentMousePosition;
                    pos = currentMousePosition;
                    span = newColSpan <= 0 ? 1 : newColSpan;
                }
                else
                {
                    // user moved right side
                    var newColSpan = currentMousePosition - prevGridPosition + 1;
                    pos = prevGridPosition;
                    span = newColSpan <= 0 ? 1 : newColSpan;
                }
            }

            Debug.Print($"{element} scaled");
        }

        private void GetPosition(out int col, out int row)
        {
            Grid control = this;
            var point = Mouse.GetPosition(control);
            row = 0;
            col = 0;
            var accumulatedHeight = 0.0;
            var accumulatedWidth = 0.0;

            // calc row mouse was over
            foreach (var rowDefinition in control.RowDefinitions)
            {
                accumulatedHeight += rowDefinition.ActualHeight;
                if (accumulatedHeight >= point.Y)
                    break;
                row++;
            }

            // calc col mouse was over
            foreach (var columnDefinition in control.ColumnDefinitions)
            {
                accumulatedWidth += columnDefinition.ActualWidth;
                if (accumulatedWidth >= point.X)
                    break;
                col++;
            }
        }

        /// <summary>
        ///     Checks if element is not null, current selected and mouse is pressed.
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        private bool ValidateElement(UIElement element)
        {
            return _isInDrag && Mouse.LeftButton == MouseButtonState.Pressed
                   && GetEditMode(element) && ReferenceEquals(element, CurrentElement) && element != null;
        }

        #endregion

        #region Inner wpf logic
        protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
        {
            // Track when objects are added and removed
            // Do stuff with the added object
            var associatedObject = visualAdded as FrameworkElement;
            if (associatedObject != null)
            {
                AddHandlersToChild(associatedObject);
            }


            base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        }

        private static void EnableDisableAll(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            var grid = dependencyObject as Grid;

            if (grid != null)
            {
                foreach (var child in grid.Children.OfType<DependencyObject>())
                {
                    child.SetValue(EditModeAttachedProperty, args.NewValue);
                }
            }
        }

        public static void SetEditMode(DependencyObject element, bool value)
        {
            element.SetValue(EditModeAttachedProperty, value);
        }

        public static bool GetEditMode(DependencyObject element)
        {
            return (bool)element.GetValue(EditModeAttachedProperty);
        }

        private void AddHandlersToChild(FrameworkElement element)
        {
            element.PreviewMouseLeftButtonDown -= root_MouseLeftButtonDown;
            element.PreviewMouseLeftButtonDown += root_MouseLeftButtonDown;
            element.PreviewMouseLeftButtonUp -= root_MouseLeftButtonUp;
            element.PreviewMouseLeftButtonUp += root_MouseLeftButtonUp;
            element.PreviewMouseMove -= root_MouseMove;
            element.PreviewMouseMove += root_MouseMove;

            if (element.RenderTransform == null)
            {
                element.RenderTransform = new TransformGroup();
                element.RenderTransform = new TransformGroup();
                ((TransformGroup)element.RenderTransform).Children.Add(new ScaleTransform());
            }
            else
            {
                if (!(element.RenderTransform is TransformGroup))
                {
                    var tr = element.RenderTransform;
                    element.RenderTransform = new TransformGroup();

                    ((TransformGroup)element.RenderTransform).Children.Add(tr);

                    if (!(tr is ScaleTransform))
                    {
                        ((TransformGroup)element.RenderTransform).Children.Add(new ScaleTransform());
                    }
                }
            }
        }
        #endregion
    }

    public struct ControlState
    {
        public object Element;

        public int Col;
        public int Row;

        public int ColSpan;
        public int RowSpan;

        public int ZIndex;
        public Thickness Margin;

        /// <summary>
        ///     Columns value where mouse started to draggin control
        /// </summary>
        public int MouseGridColumnPosition;

        /// <summary>
        ///     Row value where mouse started to draggin control
        /// </summary>
        public int MouseGridRowPosition;
    }

    public enum CollisionResult
    {
        /// <summary>
        ///     Do nothing. Can occured overlays.
        /// </summary>
        None,

        /// <summary>
        ///     If cells are not epmty, control returns to initial state
        /// </summary>
        RollbackIfCollideAny,

        /// <summary>
        ///     Raised custom event <see cref="DragableBetweenCellsGrid.CustomCollisionPolicy" />. If not set, used
        ///     <see cref="None" />
        /// </summary>
        CustomBehavour
    }


    public static class UnsafeMethods
    {
        /// Return Type: BOOL->int  
        /// X: int  
        /// Y: int
        [DllImport("user32.dll", EntryPoint = "SetCursorPos")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetCursorPos(int x, int y);


        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(ref Win32Point pt);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Win32Point
    {
        public readonly int X;
        public readonly int Y;
    }
}