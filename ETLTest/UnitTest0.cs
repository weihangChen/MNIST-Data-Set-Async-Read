﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Concurrent;
using ConsoleApplication1.Model;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;

namespace ETLTest
{
    [TestClass]
    public class UnitTest0
    {
        private CallbackModelWrapper model;
        private CallbackModelWrapper model2;

        private FileStream ifsImages;
        private FileStream ifsLabels;

        private BinaryReader brImages;
        private BinaryReader brLabels;


        [TestInitialize]
        public void Init()
        {
            var imagePath = @"..\..\Files\t10k-images.idx3-ubyte";
            var labelPath = @"..\..\Files\t10k-labels.idx1-ubyte";

            model = new CallbackModelWrapper(imagePath, labelPath);
            model2 = new CallbackModelWrapper(imagePath, labelPath);

            ifsImages = new FileStream(imagePath,
      FileMode.Open, FileAccess.Read, FileShare.Read,
      bufferSize: 4096, useAsync: true);
            ifsLabels = new FileStream(labelPath,
       FileMode.Open, FileAccess.Read, FileShare.Read,
       bufferSize: 4096, useAsync: true);

            brLabels = new BinaryReader(ifsLabels);
            brImages = new BinaryReader(ifsImages);

            int magic1 = brImages.ReadInt32(); // discard
            int numImages = brImages.ReadInt32();
            int numRows = brImages.ReadInt32();
            int numCols = brImages.ReadInt32();

            int magic2 = brLabels.ReadInt32();
            int numLabels = brLabels.ReadInt32();

            var offset_origin_image = Convert.ToInt32(ifsImages.Position);
            var offset_origin_label = Convert.ToInt32(ifsLabels.Position);
            /////////////////////
            model.numImages = numImages;
            model.numRows = numRows;
            model.numCols = numCols;
            model.numLabels = numLabels;

            model.offset_origin_image = offset_origin_image;
            model.offset_origin_label = offset_origin_label;
            ///////////
            model2.numImages = numImages;
            model2.numRows = numRows;
            model2.numCols = numCols;
            model2.numLabels = numLabels;

            model2.offset_origin_image = offset_origin_image;
            model2.offset_origin_label = offset_origin_label;

        }



        [TestMethod]
        public async Task Sync_Async_SameResult_Test_0()
        {
            //sync

            var timer = new Stopwatch();
            timer.Start();
            long t1 = 0;
            long t2 = 0;
            const int amount = 10000;
            //sync
            model = PopulateImages(model, amount);

            timer.Stop();
            t1 = timer.ElapsedMilliseconds;

            //async
            timer.Reset();
            timer.Restart();
            model2 = await PopulateImagesAsync(model2, amount);



            timer.Stop();
            t2 = timer.ElapsedMilliseconds;


            foreach (var image in model.Images)
            {
                var image2 = model2.Images.FirstOrDefault(t => t.orderInCollection == image.orderInCollection);

                Assert.IsNotNull(image2, "sync image can't be found from async collection");
                //assert label
                Assert.IsTrue(image.label.Equals(image2.label));
                //assert pixel data
                Enumerable.Range(0, image.pixels.Length - 1).ToList().ForEach(index =>
                {
                    //await Task.Delay(20);
                    Enumerable.Range(0, image.pixels[index].Length - 1).ToList().ForEach(index1 =>
                    {
                        Assert.IsTrue(image.pixels[index][index1].Equals(image2.pixels[index][index1]), string.Format("byte is different, index {0}/{1} contains different data => sync: {2} - async: {3}, image label => {4}", index, index1, image.pixels[index][index1], image2.pixels[index][index1], image.label));
                    });
                });

            }

            //really 10 times slower
            //Assert.IsTrue(t1 < t2);
        }

        #region sync method old
        private CallbackModelWrapper PopulateImages(CallbackModelWrapper model, int amount)
        {
            var pixels = model.GenerateNewPixelMatrices();
            for (int di = 0; di < amount; ++di)
            {
                for (int i = 0; i < 28; ++i)
                {
                    for (int j = 0; j < 28; ++j)
                    {
                        byte b = brImages.ReadByte();
                        pixels[i][j] = b;
                    }
                }

                byte lbl = brLabels.ReadByte();

                var image = new DigitImage(pixels, lbl, di + 1);
                model.Images.Add(image);
            }
            brImages.Close();
            brLabels.Close();
            return model;
        }
        #endregion sync method old

        #region async method old
        private async Task<CallbackModelWrapper> PopulateImagesAsync(CallbackModelWrapper model, int amount)
        {
            var offset_origin_image = model.offset_origin_image;
            var offset_origin_label = model.offset_origin_label;

            var tasks_image = Enumerable.Range(0, amount).Select(index => ReadOneImageAsync(model2, 28, offset_origin_image + index * 28 * 28, offset_origin_label + index, index + 1));
            var result = await Task.WhenAll(tasks_image);


            foreach (var image in result)
            {
                model2.Images.Add(image);
            }
            return model;
        }

        private async Task<DigitImage> ReadOneImageAsync(CallbackModelWrapper model, int imageSize, int offset_image, int offset_label, int orderInCollection)
        {
            int bufferSize = imageSize * imageSize;
            byte[] image_bytes = new byte[bufferSize];
            byte[] label_bytes = new byte[1];

            using (FileStream fsi = new FileStream(@"..\..\Files\t10k-images.idx3-ubyte",
        FileMode.Open, FileAccess.Read, FileShare.Read,
        bufferSize: 4096, useAsync: true))
            using (FileStream fsl = new FileStream(@"..\..\Files\t10k-labels.idx1-ubyte",
       FileMode.Open, FileAccess.Read, FileShare.Read,
       bufferSize: 4096, useAsync: true))
            {
                //lock (fsi)
                //{
                //    fsi.copy
                //}
                fsi.Seek(offset_image, SeekOrigin.Begin);
                fsl.Seek(offset_label, SeekOrigin.Begin);

                var tmp_label = fsl.Read(label_bytes, 0, 1);
                var tmp_image = await fsi.ReadAsync(image_bytes, 0, bufferSize);
                //var tmp_image = fsi.Read(image_bytes, 0, bufferSize);
            }

            var pixels = model.GenerateNewPixelMatrices();

            for (int i = 0; i < imageSize; ++i)
            {
                for (int j = 0; j < imageSize; ++j)
                {
                    pixels[i][j] = image_bytes[i * imageSize + j];
                }
            }



            DigitImage image = new DigitImage(pixels, label_bytes[0], orderInCollection);
            return image;
        }
        #endregion async method old


    }
}
