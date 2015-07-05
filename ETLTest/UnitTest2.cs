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
    public class UnitTest2
    {


        [TestMethod]
        public async Task ImageLoader_Test()
        {
            var imagePath = @"..\..\Files\t10k-images.idx3-ubyte";
            var labelPath = @"..\..\Files\t10k-labels.idx1-ubyte";
            var loader = new ImageLoader();
            var tmp = new CallbackModelWrapper(imagePath, labelPath);
            var model = loader.PopulateImages(tmp);

            var tmp2 = new CallbackModelWrapper(imagePath, labelPath);
            var model2 = await loader.PopulateImagesAsync(tmp2);


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

        }


    }
}
