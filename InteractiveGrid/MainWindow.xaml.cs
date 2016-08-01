using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace InteractiveGrid
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            parent.CollisionResult = CollisionResult.RollbackIfCollideAny;

            parent.FinishingWithCollisions -= RemoveAllSelections;
            parent.FinishingWithCollisions += RemoveAllSelections;

            parent.ControlChanging -= ParentFinishingWithCollisions;
            parent.ControlChanging += ParentFinishingWithCollisions;
            
            _destinationBorder = new Border { BorderThickness = new Thickness(1.0) };
            parent.Children.Add(_destinationBorder);
            DragableBetweenCellsGrid.SetSkipInCollisionCalculations(_destinationBorder, true);
        }

        private Border _destinationBorder = null;

        private void RemoveAllSelections(object sender, StateChangedEventArgs args)
        {
            _destinationBorder.Visibility = Visibility.Collapsed;
            foreach (var source in args.AllElements)
            {
                var layer = AdornerLayer.GetAdornerLayer(source);
                var adorner = layer?.GetAdorners(source)?.OfType<FillControl>();

                if (adorner?.Any() == true)
                {
                    foreach (var boxese in adorner)
                    {
                        layer.Remove(boxese);
                    }
                }
            }
        }

        private void ParentFinishingWithCollisions(object sender, StateChangedEventArgs args)
        {
            var uiElements = args.ElementsWithCollisions as UIElement[] ?? args.ElementsWithCollisions.ToArray();
            var collisions = uiElements;

            _destinationBorder.BorderBrush = collisions.Any() ? Brushes.Red : Brushes.Green;
            _destinationBorder.Background = collisions.Any() ? Brushes.PeachPuff : new SolidColorBrush(Color.FromArgb(150, 197, 225, 165));

            _destinationBorder.Visibility = Visibility.Visible;

            Grid.SetRow(_destinationBorder, args.FinishState.Row);
            Grid.SetRowSpan(_destinationBorder, args.FinishState.RowSpan);
            Grid.SetColumn(_destinationBorder, args.FinishState.Col);
            Grid.SetColumnSpan(_destinationBorder, args.FinishState.ColSpan);


            foreach (var confilctElement in collisions)
            {
                var layer = AdornerLayer.GetAdornerLayer(confilctElement);
                var adorner = layer?.GetAdorners(confilctElement)?.OfType<FillControl>();
                if (adorner == null || !adorner.Any())
                    AdornerLayer.GetAdornerLayer(confilctElement)?.Add(new FillControl(confilctElement));
            }

            foreach (var source in args.AllElements.Except(uiElements))
            {
                var layer = AdornerLayer.GetAdornerLayer(source);
                var adorner = layer?.GetAdorners(source)?.OfType<FillControl>();

                if (adorner?.Any() == true)
                {
                    foreach (var boxese in adorner)
                    {
                        layer.Remove(boxese);
                    }
                }
            }
        }

        private class FillControl : Adorner
        {
            public FillControl(UIElement adornedElement) :
                base(adornedElement)
            {
            }

            protected override void OnRender(DrawingContext drawingContext)
            {
                drawingContext.DrawRectangle(new SolidColorBrush(Color.FromArgb(70, 217, 82, 84)), null,
                    new Rect(0, 0, ActualWidth, ActualHeight));
            }
        }
    }
}
