using ConsoleApplication1.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApplication1.Service
{
    public interface IImageLoader
    {
        CallbackModelWrapper PopulateImages(CallbackModelWrapper model);
        Task<CallbackModelWrapper> PopulateImagesAsync(CallbackModelWrapper model);
    }
}
