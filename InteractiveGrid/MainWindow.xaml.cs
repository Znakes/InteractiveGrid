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
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            parent.EditModeEnabled = !parent.EditModeEnabled;
        }

        private void ButtonBase_OnClick2(object sender, RoutedEventArgs e)
        {
            parent.CollisionResult = CollisionResult.CustomBehavour;
            parent.CustomCollisionPolicy -= Parent_CustomCollisionPolicy;
            parent.CustomCollisionPolicy += Parent_CustomCollisionPolicy;
        }

        private void Parent_CustomCollisionPolicy(ControlState arg1, ControlState arg2, IEnumerable<UIElement> arg3)
        {
            MessageBox.Show(String.Join("\n", arg3));
        }
    }
}
