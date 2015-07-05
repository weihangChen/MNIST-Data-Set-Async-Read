using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Concurrent;
using ConsoleApplication1.Model;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Diagnostics;

namespace ETLTest
{
    [TestClass]
    public class UnitTest1
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
            ifsImages = new FileStream(@"..\..\Files\t10k-images.idx3-ubyte",
       FileMode.Open, FileAccess.Read, FileShare.Read,
       bufferSize: 4096, useAsync: true);
            ifsLabels = new FileStream(@"..\..\Files\t10k-labels.idx1-ubyte",
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


            model = new CallbackModelWrapper(numImages, numRows, numCols, numLabels, offset_origin_image, offset_origin_label);
            model2 = new CallbackModelWrapper(numImages, numRows, numCols, numLabels, offset_origin_image, offset_origin_label);


        }





        /// <summary>
        /// test if byte data read from BinaryReader and FileStream is same
        /// </summary>
        [TestMethod]
        public void BinaryReader_Stream_SameResult_Test()
        {
            const int amount = 1000;
            const string file = @"..\..\Files\t10k-labels.idx1-ubyte";
            var br = new BinaryReader(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read));


            var br_bytes = br.ReadBytes(amount);
            br.Close();


            var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);


            var fs_bytes = new byte[amount];
            //test first 5000 bytes
            for (int i = 0; i < amount; ++i)
            {
                var tmp = fs.ReadByte();
                if (tmp == -1)
                {
                    Assert.Fail("not same account of bytes");
                }
                var b1 = (byte)tmp;
                var b2 = br_bytes[i];
                //compare
                Assert.IsTrue(b1.Equals(b2));
            }
            fs.Close();
            br.Close();
        }




        [TestMethod]
        public async Task Sync_Async_SameResult_Test()
        {
            long time1 = 0;
            long time2 = 0;

            //sync
            var timer = Stopwatch.StartNew();
            int amount = 5;
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
            brLabels.Close();
            brImages.Close();
            timer.Stop();
            time1 = timer.ElapsedMilliseconds;

            //------------------------------------------
            //async
            timer.Restart();
            model2 = await PopulateImagesAsync(model2, amount);
            timer.Stop();
            time2 = timer.ElapsedMilliseconds;

            var diff = time2 - time1;
            //Assert.IsTrue(diff < 0, string.Format("multi thread is even slower than single thread, {0}", diff));
            foreach (var image in model.Images)
            {
                var image2 = model2.Images.First(t => t.orderInCollection == image.orderInCollection);

                Assert.IsNotNull(image2, "sync image can't be found from async collection");
                //assert label
                Assert.IsTrue(image.label.Equals(image2.label),"label is different");
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
        }


        private async Task<CallbackModelWrapper> PopulateImagesAsync(CallbackModelWrapper model, int amount)
        {
            IEnumerable<Task<DigitImage>> tasks_image = new List<Task<DigitImage>>();

            var offset_origin_image = model.offset_origin_image;
            var offset_origin_label = model.offset_origin_label;
            using (FileStream fsi = new FileStream(@"..\..\Files\t10k-images.idx3-ubyte",
       FileMode.Open, FileAccess.Read, FileShare.Read,
       bufferSize: 4096, useAsync: true))
            using (FileStream fsl = new FileStream(@"..\..\Files\t10k-labels.idx1-ubyte",
       FileMode.Open, FileAccess.Read, FileShare.Read,
       bufferSize: 4096, useAsync: true))
            {
                tasks_image = Enumerable.Range(0, amount).Select(index => ReadOneImage(model2, 28, offset_origin_image + index * 28 * 28, offset_origin_label + index, index + 1, fsi, fsl));
                var images = await Task.WhenAll(tasks_image);

                foreach (var image in images)
                {
                    model2.Images.Add(image);
                }
            }
            
            return model;
        }

        private async Task<DigitImage> ReadOneImage(CallbackModelWrapper model, int imageSize, int offset_image, int offset_label, int orderInCollection, FileStream fsi, FileStream fsl)
        {
            int bufferSize = imageSize * imageSize;
            byte[] image_bytes = new byte[bufferSize];
            byte[] label_bytes = new byte[1];

            fsi.Seek(offset_image, SeekOrigin.Begin);
            fsl.Seek(offset_label, SeekOrigin.Begin);

            var tmp_label = fsl.Read(label_bytes, 0, 1);
            //var tmp_image = await fsi.ReadAsync(image_bytes, 0, bufferSize);
            var tmp_image = fsi.Read(image_bytes, 0, bufferSize);


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


    }
}
