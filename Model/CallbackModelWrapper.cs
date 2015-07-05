using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApplication1.Model
{
    public class CallbackModelWrapper
    {
        public int numImages { get; set; }
        public int numRows { get; set; }
        public int numCols { get; set; }
        public int numLabels { get; set; }
        public int offset_origin_image { get; set; }
        public int offset_origin_label { get; set; }
        public int index { get; set; }

        public string imagePath { get; set; }
        public string labelPath { get; set; }

        private const int imageSize = 28;
        public int ImageSize
        {
            get { return imageSize; }
        }




        public ConcurrentBag<DigitImage> Images { get; set; }

        public byte[][] GenerateNewPixelMatrices()
        {
            byte[][] pixels = new byte[28][];
            for (int i = 0; i < pixels.Length; ++i)
                pixels[i] = new byte[28];
            return pixels;
        }

        //public byte[][] pixels { get; set; }
        //public CallbackModelWrapper(int numImages, int numRows, int numCols, int numLabels, int offset_origin_image, int offset_origin_label)
        //{


        //    this.numImages = numImages;
        //    this.numRows = numRows;
        //    this.numCols = numCols;
        //    this.numLabels = numLabels;
        //    this.offset_origin_image = offset_origin_image;
        //    this.offset_origin_label = offset_origin_label;
        //    Images = new ConcurrentBag<DigitImage>();
        //}

        public CallbackModelWrapper(string imagePath, string labelPath)
        {


            //this.numImages = numImages;
            //this.numRows = numRows;
            //this.numCols = numCols;
            //this.numLabels = numLabels;
            //this.offset_origin_image = offset_origin_image;
            //this.offset_origin_label = offset_origin_label;




            this.imagePath = imagePath;
            this.labelPath = labelPath;


            Images = new ConcurrentBag<DigitImage>();
        }



    }
}
