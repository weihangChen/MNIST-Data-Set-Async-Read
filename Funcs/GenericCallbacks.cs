using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApplication1.Funcs
{
    class GenericCallbacks
    {
        public static T ExecuteSafe<Inputs, T>(Func<Inputs, T> CoreFun, Inputs inputs, Action<Exception> OnErrorFun = null, Action FinallyFun = null)
        {
            Object obj = null;
            try
            {
                obj = CoreFun(inputs);
            }
            catch (Exception exception)
            {
                if (OnErrorFun != null)
                {

                    OnErrorFun(exception);

                }
                else
                {
                    throw exception;
                    //logger.Error(exception.ToString());
                }
            }
            finally
            {
                if (FinallyFun != null)
                    FinallyFun();
            }
            return (T)obj;
        }

        public static void ExecuteSafeAction(Action CoreFun, Action<Exception> OnErrorFun = null, Action FinallyFun = null)
        {

            try
            {
                CoreFun();
            }
            catch (Exception exception)
            {
                if (OnErrorFun != null)
                {

                    OnErrorFun(exception);

                }
                else
                {
                    throw exception;
                    //logger.Error(exception.ToString());
                }
            }
            finally
            {
                if (FinallyFun != null)
                    FinallyFun();
            }
        }


    }
}
