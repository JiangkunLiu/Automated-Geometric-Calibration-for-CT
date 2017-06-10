using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Prism.Commands;
using System.Windows.Input;
using System.IO;
using AutoGeometricCalibrationCT.Model;

namespace AutoGeometricCalibrationCT.ViewModel
{
    public class MainWindowModel
    {
        public String FilePath;

        /// <summary>
        /// Gets the open command.
        /// </summary>
        public ICommand OpenCommand { get; private set; }

        /// <summary>
        /// Gets the start command.
        /// </summary>
        public ICommand StartCommand { get; private set; }

        /// <summary>
        /// Gets the cancel command.
        /// </summary>
        public ICommand CancelCommand { get; private set; }

        private GeometryCalculation m_GeometricCalculation;

        public MainWindowModel()
        {
            this.OpenCommand = new DelegateCommand(this.ExecuteOpenCommand);
            this.StartCommand = new DelegateCommand(this.ExecuteStartCommand);
            this.CancelCommand = new DelegateCommand(this.ExecuteCancelCommand);
            m_GeometricCalculation = new GeometryCalculation();
            FilePath = @"D:\Geometric Calibration\R_1.3.6.1.4.1.39669.1988421.5488844675912942";
        }

        /// <summary>
        /// Executes the <see cref="OpenCommand"> command.
        /// </summary>
        private void ExecuteOpenCommand()
        {
            //Select image with a open file dialog
            using (System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog())
            {
                ofd.Filter = "All DICOM Files(*.dcm)|*.dcm";
                ofd.Title = "Select Image";
                ofd.InitialDirectory = @"D:\Geometric Calibration\R_1.3.6.1.4.1.39669.1988421.5488844675912942";

                if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    FilePath = Directory.GetParent(ofd.FileName).ToString();
                }
            }
        }

        /// <summary>
        /// Executes the <see cref="StartCommand"> command.
        /// </summary>
        private void ExecuteStartCommand()
        {
            m_GeometricCalculation.FilePath = FilePath;
            m_GeometricCalculation.CalculateGeometry();
        }

        /// <summary>
        /// Executes the <see cref="CancelCommand"> command.
        /// </summary>
        private void ExecuteCancelCommand()
        {
            m_GeometricCalculation.CancelBackgroundWorker();
        }
    }
}
