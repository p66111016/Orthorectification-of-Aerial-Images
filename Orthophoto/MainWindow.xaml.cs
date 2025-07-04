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
using ImageProcessing;
using BitMiracle.LibTiff.Classic;
using System.Diagnostics;                        //可使用Trace.Write用作debug
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Single;
using System.IO;  //Stream reader
using Microsoft.Win32;

namespace Orthophoto
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            base_path = AppDomain.CurrentDomain.BaseDirectory;

            option = base_path.Split(new[] { "\\Orthophoto\\" }, StringSplitOptions.None)[0] + "\\Aerial_data";
            output_path = base_path.Split(new[] { "\\Orthophoto\\" }, StringSplitOptions.None)[0] + "\\result";
        }

        // string option = @"C:\Users\User\Desktop\Orthorectification_of_Aerial_Images";
        string output_path;

        string base_path;

        string option;

        string fileName;

        public LOCImage OriginImage;
        private void Open_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "image file(*.jpg,*.png,*.tiff)|*.jpg;*.png;*.tiff";
            if (ofd.ShowDialog() == true)
            {
                original.Source = new BitmapImage(new Uri(ofd.FileName));

                OriginImage = new LOCImage(ofd.FileName, Int32Rect.Empty);

            }
        }


        public static double Deg2Rad(double degrees)          //角度轉弧度
        {
            double radians = (Math.PI / 180) * degrees;
            return (radians);
        }


        float Elevationmean = 0;
        double[,] Eop = new double[4, 15];
        int eopindex = 0;
        Matrix<double> Rotateomega = Matrix<double>.Build.Dense(3, 3);
        Matrix<double> Rotatephi = Matrix<double>.Build.Dense(3, 3);
        Matrix<double> Rotatekappa = Matrix<double>.Build.Dense(3, 3);
        double Pixel_X, Pixel_Y, UL_X, UL_Y;
        float[,] public_Elevation;       
        private void Read_Click(object sender, RoutedEventArgs e)
        {
            #region read dsm
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "(*.tif)|*.tif";
            if (ofd.ShowDialog() == true)
            {
                fileName = ofd.FileName;
            }
            //string fileName = @"D:\專題下\航拍\ZiChiang_DSM_10cm-largearea.tif";"image file(*.jpg,*.png,*.tiff)|*.jpg;*.png;*.tiff"
            Tiff DEM = Tiff.Open(fileName, "r");
            int Width = DEM.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
            int Height = DEM.GetField(TiffTag.IMAGELENGTH)[0].ToInt();


            FieldValue[] PixelScaleTag = DEM.GetField(TiffTag.GEOTIFF_MODELPIXELSCALETAG);
            FieldValue[] TiePointTag = DEM.GetField(TiffTag.GEOTIFF_MODELTIEPOINTTAG);

            byte[] PixelScale = PixelScaleTag[1].GetBytes();
            byte[] Transformation = TiePointTag[1].GetBytes();

            // DEM在(X,Y)方向的解析度
            Pixel_X = BitConverter.ToDouble(PixelScale, 0);
            Pixel_Y = BitConverter.ToDouble(PixelScale, 8);


            // DEM左上角(X,Y)座標
            UL_X = BitConverter.ToDouble(Transformation, 24);
            UL_Y = BitConverter.ToDouble(Transformation, 32);


            FieldValue[] bitsPerSampleTag = DEM.GetField(TiffTag.BITSPERSAMPLE);
            int bytesPerSample = bitsPerSampleTag[0].ToInt() / 8;
            //Trace.WriteLine(bytesPerSample);
            //以影像原點紀錄的高程資訊，所以實際上
            // X = UL_X + Pixel_X * (i + 0.5);
            // Y = UL_Y - Pixel_Y * (j + 0.5);  //-?
            // 請思考為什麼會有 + 0.5??     //因為UL_X、UL_Y是像點的左上角

            float[,] Elevation = new float[Height, Width];

            for (int j = 0; j < Height; j++)                   //i,j對調
            {
                var Scanline = new byte[DEM.ScanlineSize()];
                DEM.ReadScanline(Scanline, j);

                for (int i = 0; i < Width; i++)
                {
                    Elevation[j, i] = BitConverter.ToSingle(Scanline, i * bytesPerSample);
                }
            }

            public_Elevation = Elevation;
            Trace.WriteLine(public_Elevation.GetLength(1));
            Trace.WriteLine(public_Elevation.GetLength(0));

            for (int j = 0; j < Height; j++)
            {
                for (int i = 0; i < Width; i++)
                {
                    Elevationmean = Elevationmean + Elevation[j, i];
                }
            }             
            Elevationmean = (float)Math.Round(Elevationmean / Elevation.Length, MidpointRounding.AwayFromZero);
            #endregion


            Trace.WriteLine("UL_X " + UL_X);
            Trace.WriteLine("UL_Y " + UL_Y);


            //讀取外方位元素
            combobox.Items.Clear();
            #region readallline
            string[] lines = System.IO.File.ReadAllLines(option + "\\20181201-HB-NCKU-EOPs_TWD97.txt");
            for (int i = 2; i < 6; i++)
            {
                string[] substr = lines[i].Split((char)9); //按,逗号分割
                //Trace.WriteLine(lines[i]);

                combobox.Items.Add(substr[0]);
                for (int word = 1; word < 16; word++)
                {
                    Eop[i - 2, word - 1] = float.Parse(substr[word]);

                    //Trace.WriteLine(Eop[i - 2, word - 1]);

                }
            }
            #endregion

                     
        }
        

        Matrix<double> Corner = Matrix<double>.Build.Dense(4, 2);
        Matrix<double> IOP = Matrix<double>.Build.Dense(12,1);
        Matrix<double> Rotate= Matrix<double>.Build.Dense(3, 3);
        double mX, mY , Xa ,Ya;              //像空間座標 原點移到中間
        double x_max, x_min, y_max, y_min;    //Top down 物空間座標極大極小值
        bool check = true;
        public LOCImage ProcessImage;

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)           //下拉選單選取外方位元素
        {
            eopindex = combobox.SelectedIndex;          
        }

        float gsd = 0.1f;
        double X, Y;
        double dx, dy;
        double A, B, C, D;
        double bytedata ;
        int OriginIndex ;                  //原影像座標index
        int ProcessIndex;          //處裡後影像座標index
        double Objectcoordnate_x, Objectcoordnate_y;   //物空間座標 用作反算取值
        private void transform_Click(object sender, RoutedEventArgs e)
        {
        
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    Rotate[i, j] = Eop[eopindex, 6 + 3 * i + j];
                }
            }
            //Trace.WriteLine(Rotate);
            Rotate = Rotate.Inverse();
            //Trace.WriteLine(Rotate);

            #region Dostortion
            StreamReader dissr = new StreamReader(option + "\\20181201-HB-NCKU-IOPs.txt");
            for (int i = 0; i < 14; i++)
            {
                dissr.ReadLine();
            }

            string distort = "";
            for (int m = 0; m < 2; m++)               //讀取Pixel Size 存於Distortion[10, 0],Distortion[11, 0]
            {
                for (int i = 0; i < 2; i++)
                {
                    while ((char)dissr.Peek() == ' ')
                    {
                        dissr.Read();
                    }

                    while ((char)dissr.Peek() != ' ')
                    {
                        dissr.Read();
                    }
                }
                while ((char)dissr.Peek() == ' ')
                {
                    dissr.Read();
                }

                while ((char)dissr.Peek() != '\r')
                {
                    distort = distort + (char)dissr.Read();
                }

                dissr.ReadLine();

                IOP[m + 10, 0] = float.Parse(distort);
                distort = "";
            }

            for (int m = 0; m < 3; m++)          //跳過中間三行
            {
                dissr.ReadLine();
            }

            for (int m = 0; m < 10; m++)         //讀取透鏡畸變參數 存於Distortion[0, 0]~Distortion[9, 0]
            {
                for (int i = 0; i < 3; i++)
                {
                    while ((char)dissr.Peek() == ' ')
                    {
                        dissr.Read();
                    }

                    while ((char)dissr.Peek() != ' ')
                    {
                        dissr.Read();
                    }
                }

                while ((char)dissr.Peek() == ' ')
                {
                    dissr.Read();
                }

                while ((char)dissr.Peek() != ' ')
                {
                    distort = distort + (char)dissr.Read();
                }

                dissr.ReadLine();

                IOP[m, 0] = float.Parse(distort);

                distort = "";
            }

            Trace.WriteLine(IOP);
            #endregion

            DateTime time_start = DateTime.Now;   //計時開始 取得目前時間

            #region Top down
            Corner[0, 0] = 1;
            Corner[0, 1] = OriginImage.Height;
            Corner[1, 0] = OriginImage.Width;
            Corner[1, 1] = OriginImage.Height;
            Corner[2, 0] = OriginImage.Width;
            Corner[2, 1] = 1;
            Corner[3, 0] = 1;
            Corner[3, 1] = 1;

            #region  observatoin Distortion
            //座標透鏡畸變改正
            double r, X_bar, Y_bar, radial_x, radial_y, decenter_x, decenter_y;                    //將座標中心移動到影像中心、影像點到中心之距離
            for (int i = 0; i < 4; i++)
            {

                mX = Corner[i, 0] - (double)(OriginImage.Width + 1) / 2;       //座標轉換
                mY = -Corner[i, 1] + (double)(OriginImage.Height + 1) / 2;    //座標轉換

                //r = Math.Sqrt(Math.Pow(mX, 2) + Math.Pow(mY, 2)) * IOP[10, 0];  //應該是錯的

                //Trace.WriteLine(X+" "+Y);
                //Trace.WriteLine(r);

                X_bar = mX * IOP[10, 0] - IOP[1, 0];
                Y_bar = mY * IOP[10, 0] - IOP[2, 0];

                r = Math.Sqrt(Math.Pow(X_bar, 2) + Math.Pow(Y_bar, 2));    //這才對 要減掉像主點偏差

                radial_x = X_bar * (IOP[3, 0] * Math.Pow(r, 2) + IOP[4, 0] * Math.Pow(r, 4) + IOP[5, 0] * Math.Pow(r, 6));
                radial_y = Y_bar * (IOP[3, 0] * Math.Pow(r, 2) + IOP[4, 0] * Math.Pow(r, 4) + IOP[5, 0] * Math.Pow(r, 6));

                decenter_x = IOP[6, 0] * (Math.Pow(r, 2) + 2 * Math.Pow(X_bar, 2)) + 2 * IOP[7, 0] * X_bar * Y_bar + IOP[8, 0] * X_bar + IOP[9, 0] * Y_bar;
                decenter_y = 2 * IOP[6, 0] * X_bar * Y_bar + IOP[7, 0] * (Math.Pow(r, 2) + 2 * Math.Pow(Y_bar, 2));

                Corner[i, 0] = (float)(mX + radial_x / IOP[10, 0] + decenter_x / IOP[10, 0] );
                Corner[i, 1] = (float)(mY + radial_y / IOP[10, 0] + decenter_y / IOP[10, 0] );

                Trace.WriteLine(radial_x + " " + radial_y + " " + decenter_x + " " + decenter_y + " " + X_bar + "  " + Y_bar + "  " + r);

                //Trace.WriteLine(mX+"//////"+ mY+"///////////"+OriginImage.Width / 2+ "//////////"+ Procoordinate[i, 0]+"////"+r);
            }
            //座標透鏡畸變改正
            #endregion

            for (int i = 0; i < 4; i++)
            {
                mX = Corner[i, 0];   //座標轉換
                mY = Corner[i, 1];  //座標轉換

                mX = mX * IOP[10, 0];            //Pixel 轉 mm
                mY = mY * IOP[11, 0];            //Pixel 轉 mm


                Xa = Eop[eopindex, 0] + (Elevationmean - Eop[eopindex, 2]) *
                    (Rotate[0, 0] * (mX - IOP[1, 0]) + Rotate[0, 1] * (mY - IOP[2, 0]) + Rotate[0, 2] * -IOP[0, 0]) /
                    (Rotate[2, 0] * (mX - IOP[1, 0]) + Rotate[2, 1] * (mY - IOP[2, 0]) + Rotate[2, 2] * -IOP[0, 0]);

                Ya = Eop[eopindex, 1] + (Elevationmean - Eop[eopindex, 2]) *
                    (Rotate[1, 0] * (mX - IOP[1, 0]) + Rotate[1, 1] * (mY - IOP[2, 0]) + Rotate[1, 2] * -IOP[0, 0]) /
                    (Rotate[2, 0] * (mX - IOP[1, 0]) + Rotate[2, 1] * (mY - IOP[2, 0]) + Rotate[2, 2] * -IOP[0, 0]);

                Trace.WriteLine("mX " + mX);
                Trace.WriteLine("mY " + mY);
                Trace.WriteLine("Xa " + Xa);
                Trace.WriteLine("Ya " + Ya);




                if (check)
                {
                    x_max = Xa; x_min = Ya; y_max = Xa; y_min = Ya;     //設定初始值比大小
                    check = false;
                }
                                
                if (Xa > x_max)
                    x_max = Xa;
                if (Xa < x_min)
                    x_min = Xa;
                if (Ya > y_max)
                    y_max = Ya;
                if (Ya < y_min)
                    y_min = Ya;            
            }

            //double c = (x_max - x_min);
            //double b = y_max - y_min;
            Trace.WriteLine("max min: " + x_max + " " + x_min + " " + y_max + " " + y_min);
            //Trace.WriteLine("max min: " + c+ " " + b);

            #endregion

            gsd = (float)(IOP[10, 0]/1000 * Eop[eopindex, 2]  / (IOP[0, 0] / 1000));
            gsd = (float)Math.Round(gsd, 2 , MidpointRounding.AwayFromZero);

            Trace.WriteLine(IOP[10, 0]+"  " + Eop[eopindex, 2] + "    "+ IOP[0, 0] / 1000 +"  "+gsd);

            ProcessImage = new LOCImage((int)((x_max - x_min)/gsd), (int)((y_max - y_min)/ gsd), 96, 96, PixelFormats.Bgr24, null);
         
            Rotate = Rotate.Inverse();
            Objectcoordnate_x = x_min - gsd;
            Objectcoordnate_y = y_max + gsd;
            double dsmX, dsmY, dsmZ=0;    //內插高程座標
            double r_pos, X_ini, Y_ini;
            for (int j = 0; j < ProcessImage.Height; j++)
            {
                Objectcoordnate_y -= gsd;
                Objectcoordnate_x = x_min - gsd;
                for (int i = 0; i < ProcessImage.Width; i++)
                {
                    Objectcoordnate_x += gsd;

                    #region elevation Interpolate
                    dsmX = (Objectcoordnate_x - UL_X) / Pixel_X - 0.5;
                    dsmY = (Objectcoordnate_y - UL_Y) / -Pixel_Y -0.5;

                    dx = dsmX - (int)(dsmX);
                    dy = dsmY - (int)(dsmY);

                    if ( dsmX < 0 || dsmX >= public_Elevation.GetLength(1) - 1 || dsmY < 0 || dsmY >= public_Elevation.GetLength(0) - 1)
                    {                      
                        continue;                      
                    }

                    A = public_Elevation[(int)(dsmY), (int)(dsmX)];
                    B = public_Elevation[(int)(dsmY), (int)(dsmX) + 1];
                    C = public_Elevation[(int)(dsmY) + 1, (int)(dsmX)];
                    D = public_Elevation[(int)(dsmY) + 1, (int)(dsmX) + 1];

                    dsmZ  = (A + (B - A) * dx + (C - A) * dy + (A - B - C + D) * dx * dy);
                    #endregion

                    #region 座標反算取值
                    X = IOP[1, 0] + -IOP[0, 0]*
                        (Rotate[0, 0] * (Objectcoordnate_x - Eop[eopindex, 0]) + Rotate[0, 1] * (Objectcoordnate_y - Eop[eopindex, 1]) + Rotate[0, 2] * (dsmZ - Eop[eopindex, 2])) /
                        (Rotate[2, 0] * (Objectcoordnate_x - Eop[eopindex, 0]) + Rotate[2, 1] * (Objectcoordnate_y - Eop[eopindex, 1]) + Rotate[2, 2] * (dsmZ - Eop[eopindex, 2]));

                    Y = IOP[2, 0] + -IOP[0, 0] *
                        (Rotate[1, 0] * (Objectcoordnate_x - Eop[eopindex, 0]) + Rotate[1, 1] * (Objectcoordnate_y - Eop[eopindex, 1]) + Rotate[1, 2] * (dsmZ - Eop[eopindex, 2])) /
                        (Rotate[2, 0] * (Objectcoordnate_x - Eop[eopindex, 0]) + Rotate[2, 1] * (Objectcoordnate_y - Eop[eopindex, 1]) + Rotate[2, 2] * (dsmZ - Eop[eopindex, 2]));

                    //X = X / IOP[10, 0];
                    //Y = Y / IOP[10, 0];

                    #region Lens distortoion
                    //X = X * IOP[10, 0];   //單位轉換 pixel轉 mm
                    //Y = Y * IOP[10, 0];

                    X_ini = X;
                    Y_ini = Y;

                    r = Math.Sqrt(Math.Pow(X - IOP[1, 0], 2) + Math.Pow(Y - IOP[2, 0], 2));
                    r_pos = 0;

                    while (Math.Abs(r - r_pos) > 0.000001)
                    {
                        r_pos = r;

                        X_bar = X - IOP[1, 0];
                        Y_bar = Y - IOP[2, 0];

                        radial_x = X_bar * (IOP[3, 0] * Math.Pow(r, 2) + IOP[4, 0] * Math.Pow(r, 4) + IOP[5, 0] * Math.Pow(r, 6));
                        radial_y = Y_bar * (IOP[3, 0] * Math.Pow(r, 2) + IOP[4, 0] * Math.Pow(r, 4) + IOP[5, 0] * Math.Pow(r, 6));

                        decenter_x = IOP[6, 0] * (Math.Pow(r, 2) + 2 * Math.Pow(X_bar, 2)) + 2 * IOP[7, 0] * X_bar * Y_bar + IOP[8, 0] * X_bar + IOP[9, 0] * Y_bar;
                        decenter_y = 2 * IOP[6, 0] * X_bar * Y_bar + IOP[7, 0] * (Math.Pow(r, 2) + 2 * Math.Pow(Y_bar, 2));

                        X = X_ini - radial_x - decenter_x;
                        Y = Y_ini - radial_y - decenter_y;

                        r = Math.Sqrt(Math.Pow(X - IOP[1, 0], 2) + Math.Pow(Y - IOP[2, 0], 2));
                    }

                    X = X / IOP[10, 0];
                    Y = Y / IOP[10, 0];
                    #endregion

                    X = X + (double)(OriginImage.Width ) / 2;       //座標轉換
                    Y = -Y + (double)(OriginImage.Height ) / 2;    //座標轉換

                    dx = X - (int)(X);
                    dy = Y - (int)(Y);

                    if (0 <= X && X + 1 < OriginImage.Width && 0 <= Y && Y + 1 < OriginImage.Height)
                    {
                        OriginIndex = ((int)(Y) * OriginImage.Width + (int)(X));


                        for (int k = 0; k < 3; k++)
                        {
                            A = (double)(OriginImage.ByteData[OriginIndex * 3 + k]);
                            B = (double)(OriginImage.ByteData[OriginIndex * 3 + k + 3]);
                            C = (double)(OriginImage.ByteData[OriginIndex * 3 + k + OriginImage.Width * 3]);
                            D = (double)(OriginImage.ByteData[OriginIndex * 3 + k + OriginImage.Width * 3 + 3]);

                            bytedata = (A + (B - A) * dx + (C - A) * dy + (A - B - C + D) * dx * dy);

                            ProcessIndex = j * ProcessImage.Width + i;

                            ProcessImage.ByteData[ProcessIndex * 3 + k] = (byte)(bytedata);
                        }
                    }
                    #endregion
                }
            }

            DateTime time_end = DateTime.Now;//計時結束 取得目前時間
            double processtime = ((TimeSpan)(time_end - time_start)).TotalSeconds;
            

            
            if (!Directory.Exists(output_path))
            {
                Directory.CreateDirectory(output_path);
            }
       

            string ProcessFile_path = output_path + "\\Process.tif";
            ProcessImage.Save(ProcessFile_path, ImageFormat.Tiff);

            using (var stream = new FileStream(ProcessFile_path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                process.Source = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            }

            #region twf
            string path = output_path + "\\Process.tfw";
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            using (StreamWriter sw = new StreamWriter(path))
            {
                
                sw.WriteLine(gsd);
                sw.WriteLine("0");
                sw.WriteLine("0");
                sw.WriteLine(-gsd);
                sw.WriteLine(x_min.ToString("0.000"));
                sw.WriteLine(y_max.ToString("0.000"));

                sw.Close();
                sw.Dispose();

            }
            #endregion

            #region result_output
            path = output_path + "\\result.txt";

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            using (StreamWriter sw = new StreamWriter(path))
            {

                sw.WriteLine("processtime: " + processtime + " second");

                processtime = processtime / (ProcessImage.Width * ProcessImage.Height) * 1000000;

                sw.WriteLine("million pixel processtime: " + processtime + " second");

                sw.Close();
                sw.Dispose();
            }                                       
            #endregion
        }


    }
}
