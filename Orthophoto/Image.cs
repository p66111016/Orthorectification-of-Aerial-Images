using System;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;



    namespace ImageProcessing
    {
    public enum ImageFormat { Jpeg, Tiff, Bmp, Png, Wmp, Gif }

    public class LOCImage : IDisposable
    {
        public string FileName;
        public int Width, Height, NumberOfBytes, NumberOfBands, OffsetWidth, OffsetHeight, ByteBands, Stride;
        public byte[] ByteData;
        public byte[,,] ByteData2D;
        public double DpiX, DpiY;
        public PixelFormat PixelFormat;
        public BitmapMetadata Metadata;
        public BitmapSource Source;

        private bool Disposed = false;


        public LOCImage()
        { }
        public LOCImage(int Width, int Height, double DpiX, double DpiY, PixelFormat PixelFormat, BitmapMetadata Metadata)
        {
            this.Width = Width;
            this.Height = Height;
            NumberOfBands = PixelFormat.Masks.Count;
            NumberOfBytes = (PixelFormat.BitsPerPixel / NumberOfBands) / 8;
            ByteBands = NumberOfBands * NumberOfBytes;
            this.DpiX = DpiX;
            this.DpiY = DpiY;
            this.PixelFormat = PixelFormat;
            this.Metadata = Metadata;
            ByteData = new byte[Width * Height * NumberOfBytes * NumberOfBands];
        }
        public LOCImage(int Width, int Height, double DpiX, double DpiY, int NumberOfBands, byte[] ByteData)
        {
            this.Width = Width;
            this.Height = Height;
            this.NumberOfBands = NumberOfBands;
            NumberOfBytes = 1;
            ByteBands = NumberOfBands * NumberOfBytes;
            this.DpiX = DpiX;
            this.DpiY = DpiY;
            this.ByteData = ByteData;
        }


        public LOCImage(LOCImage RefImage)
        {
            Width = RefImage.Width;
            Height = RefImage.Height;

            CopyImageMetaData(RefImage);
            NumberOfBands = PixelFormat.Masks.Count;
            NumberOfBytes = (PixelFormat.BitsPerPixel / NumberOfBands) / 8;
            ByteBands = NumberOfBands * NumberOfBytes;
            Stride = Width * ByteBands;
            ByteData = new byte[Width * Height * NumberOfBytes * NumberOfBands];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="FileName"></param>
        public LOCImage(string FileName, Int32Rect Rect)
        {
            this.FileName = FileName;
            Open(FileName, Rect);
        }

        public LOCImage(BitmapSource Source, Int32Rect Rect)
        {
            GetImageInfo(Source, Rect);
            GC.Collect();
        }

        public LOCImage(string FileName, Int32Rect Rect, int band)
        {
            Open(FileName, Rect);
            byte[] Convertbyte = new byte[Width * Height];
            int Index = 0;
            for (int i = 0; i < Width; i++)
            {
                for (int j = 0; j < Height; j++)
                {
                    Index = j * Width + i;
                    if (band == 4)

                    {
                        Convertbyte[Index] = (byte)((ByteData[Index * NumberOfBands + 0] + ByteData[Index * NumberOfBands + 1] + ByteData[Index * NumberOfBands + 2]) / 3);
                    }
                    else
                    {
                        Convertbyte[Index] = ByteData[Index * NumberOfBands + band];
                    }

                }
            }
            ByteData = Convertbyte;
            NumberOfBands = 1;
            ByteBands = NumberOfBytes * NumberOfBands;
            GC.Collect();
        }


        public LOCImage(BitmapSource Source, Int32Rect Rect, int band)
        {
            GetImageInfo(Source, Rect);
            byte[] Convertbyte = new byte[Width * Height];
            int Index = 0;
            for (int i = 0; i < Width; i++)
            {
                for (int j = 0; j < Height; j++)
                {
                    Index = j * Width + i;
                    if (band == 4)

                    {
                        Convertbyte[Index] = (byte)((ByteData[Index * NumberOfBands + 0] + ByteData[Index * NumberOfBands + 1] + ByteData[Index * NumberOfBands + 2]) / 3);
                    }
                    else
                    {
                        Convertbyte[Index] = ByteData[Index * NumberOfBands + band];
                    }

                }
            }
            ByteData = Convertbyte;
            NumberOfBands = 1;
            ByteBands = NumberOfBytes * NumberOfBands;
            GC.Collect();
        }

        private void Open(string FileName, Int32Rect Rect)
        {
            Source = BitmapDecoder.Create(new Uri(FileName), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            GetImageInfo(Source, Rect);

            GC.Collect();
        }

        //private void OpenFull2D(string FileName)
        //{
        //    BitmapDecoder Bitmap = BitmapDecoder.Create(new Uri(FileName), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        //    BitmapSource Source = Bitmap.Frames[0];
        //    GetImageInfo(Source);

        //    Bitmap = null;
        //    GC.Collect();
        //}




        public void Save(string FileName, ImageFormat ImageFormat)
        {
            int Stride = Width * NumberOfBands * NumberOfBytes;
            BitmapSource BitmapSource = BitmapSource.Create(Width, Height, DpiX, DpiY, PixelFormat, null, ByteData, Stride);

            using (var Stream = File.Create(FileName))
            {
                switch (ImageFormat)
                {
                    case ImageFormat.Jpeg:
                        JpegBitmapEncoder JpgImage = new JpegBitmapEncoder();
                        JpgImage.Frames.Add(BitmapFrame.Create(BitmapSource, null, Metadata, null));
                        JpgImage.QualityLevel = 90;
                        JpgImage.Save(Stream);
                        JpgImage = null;
                        break;

                    case ImageFormat.Tiff:
                        TiffBitmapEncoder TiffImage = new TiffBitmapEncoder();
                        TiffImage.Frames.Add(BitmapFrame.Create(BitmapSource, null, Metadata, null));
                        TiffImage.Compression = TiffCompressOption.None;
                        TiffImage.Save(Stream);
                        TiffImage = null;
                        break;

                    case ImageFormat.Bmp:
                        BmpBitmapEncoder BmpImage = new BmpBitmapEncoder();
                        BmpImage.Frames.Add(BitmapFrame.Create(BitmapSource, null, Metadata, null));
                        BmpImage.Save(Stream);
                        BmpImage = null;
                        break;

                    case ImageFormat.Png:
                        PngBitmapEncoder PngImage = new PngBitmapEncoder();
                        PngImage.Frames.Add(BitmapFrame.Create(BitmapSource, null, Metadata, null));
                        PngImage.Save(Stream);
                        PngImage.Interlace = PngInterlaceOption.On;
                        PngImage = null;
                        break;

                    case ImageFormat.Wmp:
                        WmpBitmapEncoder WmpImage = new WmpBitmapEncoder();
                        WmpImage.Frames.Add(BitmapFrame.Create(BitmapSource, null, Metadata, null));
                        WmpImage.Save(Stream);
                        WmpImage.Lossless = false;
                        WmpImage = null;
                        break;
                }

                BitmapSource = null;
            }

            GC.Collect();
        }

        public void Save(string FileName, Int32Rect SourceRect, ImageFormat ImageFormat)
        {
            int Stride = Width * NumberOfBands * NumberOfBytes;

            BitmapSource BitmapSource = BitmapSource.Create(Width, Height, DpiX, DpiY, PixelFormat, null, ByteData, Stride);
            BitmapSource.CopyPixels(SourceRect, ByteData, Stride, 0);
            BitmapSource = BitmapSource.Create(SourceRect.Width, SourceRect.Height, DpiX, DpiY, PixelFormat, null, ByteData, Stride);

            using (var Stream = File.Create(FileName))
            {
                switch (ImageFormat)
                {
                    case ImageFormat.Jpeg:
                        JpegBitmapEncoder JpgImage = new JpegBitmapEncoder();
                        JpgImage.Frames.Add(BitmapFrame.Create(BitmapSource, null, Metadata, null));
                        JpgImage.QualityLevel = 90;
                        JpgImage.Save(Stream);
                        JpgImage = null;
                        break;

                    case ImageFormat.Tiff:
                        TiffBitmapEncoder TiffImage = new TiffBitmapEncoder();
                        TiffImage.Frames.Add(BitmapFrame.Create(BitmapSource, null, Metadata, null));
                        TiffImage.Compression = TiffCompressOption.None;
                        TiffImage.Save(Stream);
                        TiffImage = null;
                        break;

                    case ImageFormat.Bmp:
                        BmpBitmapEncoder BmpImage = new BmpBitmapEncoder();
                        BmpImage.Frames.Add(BitmapFrame.Create(BitmapSource, null, Metadata, null));
                        BmpImage.Save(Stream);
                        BmpImage = null;
                        break;

                    case ImageFormat.Png:
                        PngBitmapEncoder PngImage = new PngBitmapEncoder();
                        PngImage.Frames.Add(BitmapFrame.Create(BitmapSource, null, Metadata, null));
                        PngImage.Save(Stream);
                        PngImage.Interlace = PngInterlaceOption.On;
                        PngImage = null;
                        break;

                    case ImageFormat.Wmp:
                        WmpBitmapEncoder WmpImage = new WmpBitmapEncoder();
                        WmpImage.Frames.Add(BitmapFrame.Create(BitmapSource, null, Metadata, null));
                        WmpImage.Save(Stream);
                        WmpImage.Lossless = false;
                        WmpImage = null;
                        break;
                }

                BitmapSource = null;
            }

            GC.Collect();
        }


        public void SaveMultiTiff(string FileName, Int32Rect SourceRect, List<byte[]> ByteList)
        {
            Stride = Width * ByteBands;
            List<BitmapSource> BitmapSourceList = new List<BitmapSource>();
            BitmapSource BitmapSource;

            for (int i = 0; i < ByteList.Count; i++)
            {
                BitmapSource = BitmapSource.Create(Width, Height, DpiX, DpiY, PixelFormat, null, ByteList[i], Stride);
                //BitmapSource.CopyPixels(SourceRect, ByteList[i], Stride, 0);
                //BitmapSource = BitmapSource.Create(SourceRect.Width, SourceRect.Height, DpiX, DpiY, PixelFormat, null, ByteList[i], Stride);

                BitmapSourceList.Add(BitmapSource);

            }

            using (var Stream = File.Create(FileName))
            {
                TiffBitmapEncoder TiffImage = new TiffBitmapEncoder();
                for (int i = 0; i < ByteList.Count; i++)
                {
                    TiffImage.Frames.Add(BitmapFrame.Create(BitmapSourceList[i]));
                }
                TiffImage.Compression = TiffCompressOption.None;
                TiffImage.Save(Stream);

                TiffImage = null;
                BitmapSourceList = null;
                BitmapSource = null;
            }

            GC.Collect();
        }

        public void SaveFLIRTiff(string FileName, Int32Rect SourceRect, List<byte[]> ByteList)
        {
            Stride = Width * NumberOfBytes;
            List<BitmapSource> BitmapSourceList = new List<BitmapSource>();
            BitmapSource BitmapSource;

            BitmapSource = BitmapSource.Create(640, 512, DpiX, DpiY, PixelFormat, null, ByteList[0], Stride);
            BitmapSource.CopyPixels(SourceRect, ByteList[0], Stride, 0);
            BitmapSourceList.Add(BitmapSource);

            BitmapSource = BitmapSource.Create(4000, 3000, DpiX, DpiY, PixelFormats.Bgr24, null, ByteList[1], 4000 * 3);
            BitmapSource.CopyPixels(SourceRect, ByteList[1], 4000 * 3, 0);
            BitmapSourceList.Add(BitmapSource);

            using (var Stream = File.Create(FileName))
            {
                TiffBitmapEncoder TiffImage = new TiffBitmapEncoder();
                for (int i = 0; i < ByteList.Count; i++)
                {
                    TiffImage.Frames.Add(BitmapFrame.Create(BitmapSourceList[i]));
                }
                TiffImage.Compression = TiffCompressOption.None;
                TiffImage.Save(Stream);

                TiffImage = null;
                BitmapSourceList = null;
                BitmapSource = null;
            }

            GC.Collect();
        }


        public void SaveMultiTiff(string FileName, List<byte[]> ByteList)
        {
            int Stride = Width * NumberOfBytes;
            List<BitmapSource> BitmapSourceList = new List<BitmapSource>();
            BitmapSource BitmapSource;

            for (int i = 0; i < ByteList.Count; i++)
            {
                BitmapSource = BitmapSource.Create(Width, Height, DpiX, DpiY, PixelFormat, null, ByteList[i], Stride);
                BitmapSource.CopyPixels(ByteList[i], Stride, 0);
                BitmapSource = BitmapSource.Create(Width, Height, DpiX, DpiY, PixelFormat, null, ByteList[i], Stride);

                BitmapSourceList.Add(BitmapSource);

            }

            using (var Stream = File.Create(FileName))
            {
                TiffBitmapEncoder TiffImage = new TiffBitmapEncoder();
                for (int i = 0; i < ByteList.Count; i++)
                {

                    TiffImage.Frames.Add(BitmapFrame.Create(BitmapSourceList[i]));
                }
                TiffImage.Compression = TiffCompressOption.None;
                TiffImage.Save(Stream);

                TiffImage = null;
                BitmapSourceList = null;
                BitmapSource = null;
            }

            GC.Collect();
        }
        public void CopyImageMetaData(LOCImage TarImage)
        {
            DpiX = TarImage.DpiX;
            DpiY = TarImage.DpiY;
            Metadata = TarImage.Metadata;
            PixelFormat = TarImage.PixelFormat;
        }

        private void GetImageInfo(BitmapSource Source, Int32Rect Rect)
        {
            NumberOfBands = Source.Format.Masks.Count;
            NumberOfBytes = (Source.Format.BitsPerPixel / NumberOfBands) / 8;
            ByteBands = NumberOfBands * NumberOfBytes;
            DpiX = Source.DpiX;
            DpiY = Source.DpiY;
            PixelFormat = Source.Format;
            Metadata = (BitmapMetadata)Source.Metadata;

            if (Rect.IsEmpty == false)
            {
                Width = Rect.Width;
                Height = Rect.Height;
                OffsetWidth = Rect.X;
                OffsetHeight = Rect.Y;
                Stride = Width * ByteBands;
                ByteData = new byte[Stride * Height];
                Source.CopyPixels(Rect, ByteData, Stride, 0);
            }
            else
            {
                Width = Source.PixelWidth;
                Height = Source.PixelHeight;
                OffsetWidth = OffsetHeight = 0;
                Stride = Width * ByteBands;
                ByteData = new byte[Stride * Height];
                Source.CopyPixels(ByteData, Stride, 0);
            }

            Source = null;
            GC.Collect();
        }


        private void GetImageInfo2D(BitmapSource Source)
        {
            Stride = 0;
            Width = Source.PixelWidth;
            Height = Source.PixelHeight;
            OffsetWidth = OffsetHeight = 0;
            DpiX = Source.DpiX;
            DpiY = Source.DpiY;
            PixelFormat = Source.Format;
            Metadata = (BitmapMetadata)Source.Metadata;

            NumberOfBands = Source.Format.Masks.Count;
            NumberOfBytes = (Source.Format.BitsPerPixel / NumberOfBands) / 8;
            ByteBands = NumberOfBands * NumberOfBytes;
            Stride = Width * NumberOfBytes * NumberOfBands;
            ByteData = new byte[Stride * Height];
            ByteData2D = new byte[Width, Height, NumberOfBands];
            Source.CopyPixels(ByteData, Stride, 0);

            int Index = 0;
            for (int i = 0; i < Width; i++)
            {
                for (int j = 0; j < Height; j++)
                {
                    Index = (j * Width + i) * NumberOfBands;
                    ByteData2D[i, j, 0] = ByteData[Index + 0];
                    ByteData2D[i, j, 1] = ByteData[Index + 1];
                    ByteData2D[i, j, 2] = ByteData[Index + 2];
                }
            }


            Source = null;
            GC.Collect();
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Disposed)
                return;

            if (disposing)
            {
                ByteData = null;
                Metadata = null;
                Source = null;
                Width = Height = 0;
                DpiX = DpiY = 0;
                NumberOfBytes = NumberOfBands = 0;
                PixelFormat = PixelFormats.Default;
                GC.Collect();
            }
            Disposed = true;
        }

        ~LOCImage()
        {
            Dispose(false);
        }


    }

}
