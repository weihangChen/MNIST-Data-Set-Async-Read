using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Concurrent;
using ConsoleApplication1.Model;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using ConsoleApplication1.Service;

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
            //sync
            var timer = new Stopwatch();
            timer.Start();
            long t1 = 0;
            long t2 = 0;

            //sync
            model = PopulateImages(model);

            timer.Stop();
            t1 = timer.ElapsedMilliseconds;

            //async
            timer.Reset();
            timer.Restart();
            model2 = await PopulateImagesAsync(model2);



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



        private CallbackModelWrapper PopulateImages(CallbackModelWrapper model)
        {
            var pixels = model.GenerateNewPixelMatrices();
            int amount = 0;


            try
            {
                while (true)
                {
                    if (amount >= 9999)
                    {
                        var h = "KK";
                        var eee = h;
                    }
                    for (int i = 0; i < 28; ++i)
                    {

                        for (int j = 0; j < 28; ++j)
                        {
                            byte b = brImages.ReadByte();

                            pixels[i][j] = b;
                        }
                    }

                    byte lbl = brLabels.ReadByte();
                    var image = new DigitImage(pixels, lbl, ++amount);
                    model.Images.Add(image);
                }
            }
            catch (Exception e)
            {
                //will get exception here
                //BinaryReader cannot read past end of file error
                Console.Write(e.ToString());
            }
            brImages.Close();
            brLabels.Close();
            return model;
        }

        private async Task<CallbackModelWrapper> PopulateImagesAsync(CallbackModelWrapper model)
        {
            var offset_origin_image = model.offset_origin_image;
            var offset_origin_label = model.offset_origin_label;

            List<Task> tasks = new List<Task>();

            Action<CallbackModelWrapper, CancellationTokenSource> Populate_AddImageAsyncFunc = (x, y) => { tasks.Add(Populate_AddImageAsync(x, y)); };
            var cts = new CancellationTokenSource();



            Task result = null;

            try
            {

                //the cancellation token will cause maybe 10003 to 10005 to cast taskcancellationexception
                //what happens to 10001 and 10002? they might enter into the code block at the same time
                //so the cancellation from 10001 might not affect 10002

                //if I specify a index 10005 here, the cancellation will yield 3 times TaskCancellation exception here, following code block is not used, since we don't really know how many images are saved within the file, use while(true) instead
                //Enumerable.Range(0, 10005).ToList().ForEach((x) =>
                //{
                //    Populate_AddImageAsyncFunc(model, cts);
                //});



                //after changing to why true, no TaskCanceledException is thrown simply because no new task will 
                //spin up when the cacellation is invoked, the ones that got canceled at the same time, they just 
                //do a peaceful return
                while (!cts.IsCancellationRequested)
                {
                    Populate_AddImageAsyncFunc(model, cts);
                }



                result = Task.WhenAll(tasks);
                await result;
            }
            catch (TaskCanceledException tce)
            {
                Console.WriteLine("is canceld");
            }
            catch (Exception e)
            {

                Console.WriteLine("Task IsFaulted: " + result.IsFaulted);
                foreach (var inEx in result.Exception.InnerExceptions)
                {
                    Debug.WriteLine("Task Inner Exception: " + inEx.Message);
                }
            }
            cts = null;



            return model;
        }


        private async Task Populate_AddImageAsync(CallbackModelWrapper model, CancellationTokenSource cts)
        {
            var index = 0;
            lock (model)
            {
                index = model.index++;
            }
            int bufferSize = model.ImageSize * model.ImageSize;
            var offset_image = model.offset_origin_image + index * bufferSize;
            var offset_label = model.offset_origin_label + index;
            var orderInCollection = index + 1;


            byte[] image_bytes = new byte[bufferSize];
            byte[] label_bytes = new byte[1];
            //
            using (FileStream fsi = new FileStream(@"..\..\Files\t10k-images.idx3-ubyte",
        FileMode.Open, FileAccess.Read, FileShare.Read,
        bufferSize: 4096, useAsync: true))
            using (FileStream fsl = new FileStream(@"..\..\Files\t10k-labels.idx1-ubyte",
       FileMode.Open, FileAccess.Read, FileShare.Read,
       bufferSize: 4096, useAsync: true))
            {
                var tmp_label = 0;
                var tmp_image = 0;
                try
                {
                    fsi.Seek(offset_image, SeekOrigin.Begin);
                    fsl.Seek(offset_label, SeekOrigin.Begin);

                    tmp_label = fsl.Read(label_bytes, 0, 1);
                    tmp_image = await fsi.ReadAsync(image_bytes, 0, bufferSize, cts.Token);
                    //var tmp_image = fsi.Read(image_bytes, 0, bufferSize);
                }
                catch (TaskCanceledException e)
                {
                    throw e;
                }
                //throw exception here, no byte can be read 
                if (tmp_label == 0 || tmp_image == 0)
                {

                    cts.Cancel();
                    return;
                    //cancellation token only yields exception for later threads
                    //to prevent the current thread continue, either throw an exception or return
                    //the behavior observed is that the taskcancellation exception can't be caught if
                    //I throw an exception here, so just do a return 
                    //throw new Exception("reach the end of stream, use token to cancel all tasks");
                }

            }
            //var t = System.Threading.Thread.CurrentThread.ManagedThreadId;
            var pixels = model.GenerateNewPixelMatrices();

            for (int i = 0; i < model.ImageSize; ++i)
            {
                for (int j = 0; j < model.ImageSize; ++j)
                {
                    pixels[i][j] = image_bytes[i * model.ImageSize + j];
                }
            }



            DigitImage image = new DigitImage(pixels, label_bytes[0], orderInCollection);
            model.Images.Add(image);
        }

    }
}
