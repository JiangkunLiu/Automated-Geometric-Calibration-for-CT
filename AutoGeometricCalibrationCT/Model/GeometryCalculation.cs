using System;
using Dicom;
using System.IO;
using System.Windows;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;

namespace AutoGeometricCalibrationCT.Model
{
    class GeometryCalculation
    {
        public String FilePath;
        public ushort[] Image { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }
        // Detector cell size 0.388, with magnification factor 0.273
        public double PixelSize = 0.273;
        public int ProjectionNumber { get; set; }
        private static BackgroundWorker m_Worker;
        public GeometryParameter GeometricParameter;
        public string[] imageNames;

        public int BeadNumber { get; set; }
        public int ProjetionStep { get; set; }
        public double[][] BeadPosition;
        public ushort RawBeamValue;

        public GeometryCalculation()
        {
            GeometricParameter = new GeometryParameter();
            m_Worker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            m_Worker.DoWork += Worker_DoWork;
            m_Worker.ProgressChanged += Worker_ProgressChanged;
            m_Worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
        }

        public void CalculateGeometry()
        {
            m_Worker.RunWorkerAsync();
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            //int progress = 0;
            // m_Worker.ReportProgress(progress);
            //if (m_Worker.CancellationPending)
            //{
            //    e.Cancel = true;
            //    return;
            //}
            //e.Result = GeometricParameter;

            Prepare();
            GetBeadPosition();
            ComputeParamter();
        }

        private void Prepare()
        {
            ProjetionStep = 1;
            BeadNumber = 2;

            imageNames = Directory.GetFiles(FilePath, "*.dcm", SearchOption.TopDirectoryOnly);
            ProjectionNumber = imageNames.Length;
            BeadPosition = new double[2][];
            BeadPosition[0] = new double[BeadNumber * ProjectionNumber / ProjetionStep]; // U
            BeadPosition[1] = new double[BeadNumber * ProjectionNumber / ProjetionStep]; // V

            // Estimate raw beam value
            int edge = 6;
            int width = 16;
            int height = 16;
            long sum = 0;
            ReadDicomData(imageNames[ProjectionNumber / 2]);
            for (int x = edge; x < edge + width; x++)
                for (int y = edge; y < edge + height; y++)
                    sum += Image[y * Width + x];
            for (int x = Width - edge; x > Width - edge - width; x--)
                for (int y = edge; y < edge + height; y++)
                    sum += Image[y * Width + x];

            RawBeamValue = (ushort)(sum / (width * height * 2));
        }

        private void GetBeadPosition()
        {
            int edge = 16;
            ushort beadThreshold = (ushort)(RawBeamValue * 0.8);
            int windowSize = 64;
            int upperLeft_X;
            int upperLeft_Y;
            int beadIndex = 0;

            for (int index = 0; index < ProjectionNumber / ProjetionStep; index++)
            {
                // Projections used have to be sampled with equal angle space
                Console.WriteLine(string.Format("Processing projection: {0:000}", index * ProjetionStep));
                ReadDicomData(imageNames[index * ProjetionStep]);
                beadIndex = 0;
                for (int y = edge; y < Height - edge; y++)
                    for (int x = edge; x < Width - edge; x++)
                    {
                        // If a target pixel is found, construct a window to check this area
                        if (Image[y * Width + x] < beadThreshold)
                        {
                            upperLeft_X = x - 10;
                            upperLeft_Y = y - 10;
                            double centerX = 0;
                            double centerY = 0;

                            int count = 0;
                            for (int xx = upperLeft_X; xx < upperLeft_X + windowSize; xx++)
                                for (int yy = upperLeft_Y; yy < upperLeft_Y + windowSize; yy++)
                                    if (xx < Width & yy < Height)
                                    {
                                        if (Image[yy * Width + xx] < beadThreshold)
                                        {
                                            Image[yy * Width + xx] = RawBeamValue;
                                            centerX += xx;
                                            centerY += yy;
                                            count++;
                                        }
                                    }

                            if (count > 36)
                            {
                                if (beadIndex >= BeadNumber)
                                {
                                    SaveShortData(@"D:\Geometric Calibration\temp.raw");
                                    throw new Exception("Bead number larger than expected.");
                                }
                                centerX /= count;
                                centerY /= count;
                                //// **************************************************************//////
                                // (u,v) detector coordinate, the origin affects the results
                                // It seems like if the origin is closer to the true raw beam center, the error is smaller
                                double uo = Width / 2;
                                double vo = Height;
                                BeadPosition[0][beadIndex * ProjectionNumber / ProjetionStep + index] = (centerX - uo) * PixelSize;
                                BeadPosition[1][beadIndex * ProjectionNumber / ProjetionStep + index] = (Height - centerY - vo) * PixelSize;
                                beadIndex++;
                                // Image[(int)centerY * Width + (int)centerX] = 20000;
                            }
                        }
                    }
                // SaveShortData(string.Format(@"D:\Geometric Calibration\temp{0:000}.raw", index * ProjetionStep));
            }
        }

        private void ComputeParamter()
        {
            //Geometric misalignment and calibration in cone-beam tomography, Smekal, et. al.
            double pi = 3.1415926;
            int N = ProjectionNumber / ProjetionStep;
            // Define real part and imaginary part of FFT result
            double[][] Ur = new double[BeadNumber][];
            double[][] Vr = new double[BeadNumber][];
            double[][] Ui = new double[BeadNumber][];
            double[][] Vi = new double[BeadNumber][];
            for (int i = 0; i < BeadNumber; i++)
            {
                Ur[i] = new double[4];
                Vr[i] = new double[4];
                Ui[i] = new double[4];
                Vi[i] = new double[4];
            }
            // Parameters
            double[] skewAngle = new double[BeadNumber]; // miu
            double[] tiltAngle = new double[BeadNumber]; // theta
            double[] slantAngle = new double[BeadNumber]; // phi
            double[] SDD = new double[BeadNumber]; // Source detector distance
            // No need to put dy here because SDD give true source detector distance
            // dy is the difference between SDD and ideal SDD
            double[] dx = new double[BeadNumber]; // Shift in x direction
            double[] dz = new double[BeadNumber]; // Shift in y direction

            // Compute fourier coefficients of order <= 3
            for (int k = 0; k < BeadNumber; k++)
            {
                for (int order = 0; order < 4; order++)
                {
                    Ur[k][order] = 0;
                    Vr[k][order] = 0;
                    Ui[k][order] = 0;
                    Vi[k][order] = 0;
                    for (int n = 0; n < N; n++)
                    {
                        Ur[k][order] += (BeadPosition[0][k * N + n] * Math.Cos(order * n * 2 * pi / N));
                        Vr[k][order] += (BeadPosition[1][k * N + n] * Math.Cos(order * n * 2 * pi / N));
                        Ui[k][order] += (BeadPosition[0][k * N + n] * Math.Sin(order * n * 2 * pi / N));
                        Vi[k][order] += (BeadPosition[1][k * N + n] * Math.Sin(order * n * 2 * pi / N));
                    }
                    Ur[k][order] *= ((double)2 / N);
                    Vr[k][order] *= ((double)2 / N);
                    Ui[k][order] *= ((double)2 / N);
                    Vi[k][order] *= ((double)2 / N);
                }
            }

            double[] d20 = new double[BeadNumber];
            double[] d21 = new double[BeadNumber];
            double[] d22 = new double[BeadNumber];
            double[] d02 = new double[BeadNumber];
            double[] d12 = new double[BeadNumber];
            double rou = 1; // Common factor, canceled out in the parameter calculation
            for (int k = 0; k < BeadNumber; k++)
            {
                d20[k] = rou * ((Ur[k][1] - Ur[k][3]) * Ur[k][2] - (Ui[k][3] - Ui[k][1]) * Ui[k][2]);
                d21[k] = rou * ((Ur[k][1] + Ur[k][3]) * Ui[k][2] - (Ui[k][3] + Ui[k][1]) * Ur[k][2]);
                d22[k] = rou * (Ur[k][3] * Ur[k][3] - Ur[k][1] * Ur[k][1] + Ui[k][3] * Ui[k][3] - Ui[k][1] * Ui[k][1]) / 2;
                d02[k] = (d20[k] * Ur[k][1] + d21[k] * Ui[k][1] + d22[k] * Ur[k][0]) / 2;
                d12[k] = (d20[k] * Vr[k][1] + d21[k] * Vi[k][1] + d22[k] * Vr[k][0]) / 2;
            }

            double[] cp01 = new double[BeadNumber];
            double[] cp11 = new double[BeadNumber];
            double[] cp00 = new double[BeadNumber];
            double[] cp10 = new double[BeadNumber];
            for (int k = 0; k < BeadNumber; k++)
            {
                double dp21 = d21[k] / d22[k];
                double dp20 = d20[k] / d22[k];
                cp01[k] = (dp21 * (0.5 * (dp20 * Ur[k][2] + dp21 * Ui[k][2]) + Ur[k][1]) - dp20 * (0.5 * (dp20 * Ui[k][2] - dp21 * Ur[k][2]) + Ui[k][1])) / (dp20 * dp20 + dp21 * dp21);
                cp11[k] = (dp21 * (0.5 * (dp20 * Vr[k][2] + dp21 * Vi[k][2]) + Vr[k][1]) - dp20 * (0.5 * (dp20 * Vi[k][2] - dp21 * Vr[k][2]) + Vi[k][1])) / (dp20 * dp20 + dp21 * dp21);
                cp00[k] = (dp20 * (0.5 * (dp20 * Ur[k][2] + dp21 * Ui[k][2]) + Ur[k][1]) + dp21 * (0.5 * (dp20 * Ui[k][2] - dp21 * Ur[k][2]) + Ui[k][1])) / (dp20 * dp20 + dp21 * dp21) + Ur[k][0] / 2;
                cp10[k] = (dp20 * (0.5 * (dp20 * Vr[k][2] + dp21 * Vi[k][2]) + Vr[k][1]) + dp21 * (0.5 * (dp20 * Vi[k][2] - dp21 * Vr[k][2]) + Vi[k][1])) / (dp20 * dp20 + dp21 * dp21) + Vr[k][0] / 2;
                skewAngle[k] = Math.Atan(-cp11[k] / cp01[k]);
            }

            double[] A = new double[BeadNumber];
            double[] B = new double[BeadNumber];
            double[] C = new double[BeadNumber];
            double[] E = new double[BeadNumber];
            double[] F = new double[BeadNumber];
            for (int k = 0; k < BeadNumber; k++)
            {
                A[k] = Math.Sin(skewAngle[k]) * cp00[k] + Math.Cos(skewAngle[k]) * cp10[k];
                B[k] = Math.Cos(skewAngle[k]) * cp01[k] - Math.Sin(skewAngle[k]) * cp11[k];
                C[k] = Math.Cos(skewAngle[k]) * cp00[k] - Math.Sin(skewAngle[k]) * cp10[k];
                E[k] = Math.Sin(skewAngle[k]) * d02[k] / d22[k] + Math.Cos(skewAngle[k]) * d12[k] / d22[k];
                F[k] = Math.Cos(skewAngle[k]) * d02[k] / d22[k] - Math.Sin(skewAngle[k]) * d12[k] / d22[k];
            }

            // Tilte angle needs at least two beans orbits to be calculated
            for (int k = 0; k < BeadNumber; k++)
            {
                double temp = 0;
                for (int j = 0; j < BeadNumber; j++)
                    if (j != k)
                    {
                        temp += (B[k] * (F[k] - F[j]) / ((E[k] - E[j]) * (C[k] - F[j]) - (F[k] - F[j]) * (A[k] - E[j])));
                    }
                tiltAngle[k] = Math.Asin(temp / (BeadNumber - 1));
            }

            for (int k = 0; k < BeadNumber; k++)
            {
                double temp = 0;
                for (int j = 0; j < BeadNumber; j++)
                {
                    temp += ((C[k] - F[j]) / (Math.Sin(tiltAngle[k]) * (A[k] - E[j]) + B[k]));
                }
                slantAngle[k] = Math.Atan(temp / BeadNumber);
            }

            for (int k = 0; k < BeadNumber; k++)
            {
                SDD[k] = Math.Sqrt(A[k] * A[k] + B[k] * B[k] + C[k] * C[k] + 2 * Math.Sin(tiltAngle[k]) * A[k] * B[k]);
            }

            for (int k = 0; k < BeadNumber; k++)
            {
                double temp = 0;
                for (int j = 0; j < BeadNumber; j++)
                {
                    temp += (Math.Sin(slantAngle[k]) * Math.Sin(tiltAngle[k]) * E[j] - Math.Cos(slantAngle[k]) * F[j]);
                }
                dx[k] = temp / BeadNumber / PixelSize;

                dz[k] = (-Math.Cos(tiltAngle[k]) * A[k]) / PixelSize;
            }

            // Calculate standard deviation of each value from all beads
            GeometricParameter.SkewAngle = StandardDeviation(skewAngle);
            GeometricParameter.SlantAngle = StandardDeviation(slantAngle);
            GeometricParameter.TiltAngle = StandardDeviation(tiltAngle);
            GeometricParameter.HeightCenter = StandardDeviation(dz);
            GeometricParameter.WidthCenter = StandardDeviation(dx);
            GeometricParameter.SDD = StandardDeviation(SDD);
            GeometricParameter.WidthCenter[0] += (double)Width / 2;

            // Print results
            Console.WriteLine(string.Format("Skew: {0} +/- {1}", GeometricParameter.SkewAngle[0], GeometricParameter.SkewAngle[1]));
            Console.WriteLine(string.Format("Tile: {0} +/- {1}", GeometricParameter.TiltAngle[0], GeometricParameter.TiltAngle[1]));
            Console.WriteLine(string.Format("Slant: {0} +/- {1}", GeometricParameter.SlantAngle[0], GeometricParameter.SlantAngle[1]));
            Console.WriteLine(string.Format("Width center: {0} +/- {1}", GeometricParameter.WidthCenter[0], GeometricParameter.WidthCenter[1]));
            Console.WriteLine(string.Format("Height center: {0} +/- {1}", GeometricParameter.HeightCenter[0], GeometricParameter.HeightCenter[1]));
            Console.WriteLine(string.Format("SDD: {0} +/- {1}", GeometricParameter.SDD[0], GeometricParameter.SDD[1]));
        }

        private double[] StandardDeviation(double[] values)
        {
            double avg = values.Average();
            return new double[] { avg, Math.Sqrt(values.Average(v => Math.Pow(v - avg, 2))) };
        }

        static void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {

            Console.WriteLine("Completed" + e.ProgressPercentage + "%");
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                Console.WriteLine("Operation Cancelled");
            }
            else if (e.Error != null)
            {
                Console.WriteLine("Error in Process :" + e.Error);
            }
            else
            {
                Console.WriteLine("Operation Completed :" + e.Result);
            }
        }

        public void CancelBackgroundWorker()
        {
            if (m_Worker.IsBusy)
            {
                m_Worker.CancelAsync();
            }
            else
            {
                Console.WriteLine("No task is in progress.");
            }
        }

        public bool ReadDicomData(string fileName)
        {
            try
            {
                DicomFile dicomFile = DicomFile.Open(fileName);
                //// Read private attributes from private blocks
                Height = dicomFile.Dataset.Get<int>(DicomTag.Parse("0771,0102"));
                Width = dicomFile.Dataset.Get<int>(DicomTag.Parse("0771,0103"));

                if (Image == null || Image.Length < Width * Height)
                {
                    Image = new ushort[Width * Height];
                }
                Image = dicomFile.Dataset.Get<ushort[]>(DicomTag.Parse("0771,0100"));
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Cannot read image: {0}", fileName), ex);
            }
        }

        static public bool ReadShortData(string fName, short[] data, int size)
        {
            byte[] tmp = new byte[size * sizeof(short)];
            if (!File.Exists(fName))
            {
                MessageBox.Show("File does not exist: " + fName, "Error (readShortData)",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            FileStream fs = File.OpenRead(fName);
            fs.Read(tmp, 0, size * sizeof(short));//only read byte data
            fs.Close();
            Buffer.BlockCopy(tmp, 0, data, 0, size * sizeof(short));
            tmp = null;
            return true;
        }

        public void SaveShortData(string fName)
        {
            byte[] tmp = new byte[Width * Height * sizeof(short)];
            Buffer.BlockCopy(Image, 0, tmp, 0, Width * Height * sizeof(short));

            // find the directory of the file and create if not existing
            string dir = fName.Substring(0, fName.LastIndexOf(@"\"));
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            FileStream fs = File.Create(fName);
            fs.Write(tmp, 0, Width * Height * sizeof(short));
            fs.Close();
            tmp = null;
        }

        public void SaveDoubleData(string fName, double[] data, int size)
        {
            byte[] tmp = new byte[size * sizeof(double)];
            Buffer.BlockCopy(data, 0, tmp, 0, size * sizeof(double));

            // find the directory of the file and create if not existing
            string dir = fName.Substring(0, fName.LastIndexOf(@"\"));
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            FileStream fs = File.Create(fName);
            fs.Write(tmp, 0, size * sizeof(double));
            fs.Close();
            tmp = null;
        }
    }
}
