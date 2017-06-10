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
using AutoGeometricCalibrationCT.ViewModel;

namespace AutoGeometricCalibrationCT.View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainWindowModel m_ViewModel;

        public MainWindowModel ViewModel
        {
            get { return m_ViewModel; }
            set
            {
                m_ViewModel = value;
                if (m_ViewModel != null)
                {
                    DataContext = m_ViewModel;
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainWindowModel();
        }
    }
}
