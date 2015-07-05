using ConsoleApplication1.Funcs;
using ConsoleApplication1.Model;
using ConsoleApplication1.Service;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApplication1
{

    class Program
    {



        static void Main(string[] args)
        {
            try
            {
                var loader = new ImageLoader();

                var imagePath = @"..\..\Files\t10k-images.idx3-ubyte";
                var labelPath = @"..\..\Files\t10k-labels.idx1-ubyte";
                var model = new CallbackModelWrapper(imagePath, labelPath);

                Func<CallbackModelWrapper, Task<CallbackModelWrapper>> coreFunc = (x) =>
                {
                    return loader.PopulateImagesAsync(x);
                };

                Action finallyAction = () => { };
                model = GenericCallbacks.ExecuteSafe(coreFunc, model, FinallyFun: finallyAction).Result;

                foreach (var image in model.Images)
                {
                    Console.WriteLine(image.ToString());
                    Console.ReadLine();
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            Console.WriteLine("\nEnd\n");
            Console.ReadLine();
        }


    }
}
